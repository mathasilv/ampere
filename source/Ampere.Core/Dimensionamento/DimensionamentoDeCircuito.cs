using Ampere.Core.Cargas;
using Ampere.Core.Catalogos;
using Ampere.Core.Memoria;
using Ampere.Core.Normas;

namespace Ampere.Core.Dimensionamento;

/// <summary>
///     Motor de dimensionamento de um circuito terminal: IB → condutores carregados → FCT → FCA → seção mínima →
///     seção pela capacidade de condução → disjuntor (IB ≤ In ≤ IZ) → queda de tensão → IDR → seções do neutro e do
///     condutor de proteção → eletroduto (ocupação).
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Toda referência vem do perfil (tabelas e regras) — o motor não cita norma por conta própria.</item>
///         <item>Faltou dado (tabela TODO_NORMA, linha ou temperatura não tabelada): o cálculo para, e a memória termina
///         num passo de resultado nulo explicando o que falta. Nada é aproximado.</item>
///         <item>Sem disjuntor coordenável ou com queda acima do limite, a seção sobe para a próxima nominal e a memória
///         registra o motivo.</item>
///         <item>Queda de tensão pela fórmula resistiva (sem reatância), só do circuito terminal.</item>
///         <item>IDR: exigido quando algum ponto está num local que a tabela do perfil manda proteger para o tipo de carga
///         do circuito; I<sub>Δn</sub> = a menor máxima entre esses locais; corrente nominal = a menor da série com
///         In(IDR) ≥ In(disjuntor). A decisão do projetista prevalece e a memória registra o que a tabela daria. Ponto sem
///         local ou local fora da tabela, sem decisão do projetista, interrompe o cálculo.</item>
///         <item>Eletroduto: menor tamanho do catálogo com n · d² / Di² dentro da taxa máxima, contando só os condutores
///         do próprio circuito (fases, neutro e proteção, todos com o diâmetro da fase). Diâmetros vêm dos catálogos de
///         fabricante; catálogo sem dados interrompe o cálculo como tabela TODO_NORMA. Só nos métodos que o perfil diz
///         levarem eletroduto (<see cref="PerfilNormativo.ComEletroduto" />).</item>
///         <item>Neutro com a seção da fase (a seção reduzida que a norma permite acima de 25 mm² não é aplicada); condutor
///         de proteção pela tabela do perfil, do mesmo material das fases.</item>
///         <item>Linha enterrada (<see cref="PerfilNormativo.Enterrado" />): a temperatura da entrada é a do solo e o
///         agrupamento sai da tabela própria do método.</item>
///         <item>Decisões do projetista (seção mínima, disjuntor): verificadas, nunca aceitas às cegas. A seção do
///         projetista é piso (abaixo da mínima da norma, vale a da norma, com aviso); o disjuntor do projetista é fixo, e a
///         seção sobe até IZ ≥ In. Seção fora das nominais ou In fora da série ou abaixo de IB interrompe o cálculo. A
///         memória registra cada decisão com a justificativa.</item>
///         <item>Aritmética em <c>decimal</c> e memória determinística.</item>
///     </list>
/// </remarks>
public static class DimensionamentoDeCircuito
{
    private const decimal Raiz3 = 1.7320508075688772935274463415m;

    public static ResultadoDoDimensionamento Dimensionar(EntradaDeDimensionamento entrada, PerfilNormativo perfil, CatalogosDeProduto catalogos)
    {
        var problemas = entrada.Validar();
        if (problemas.Count > 0)
        {
            return new ResultadoDoDimensionamento(entrada.Circuito, SituacaoDoDimensionamento.EntradaInvalida, perfil.Nome,
                null, null, null, null, null, null, null, null, null, null, null, null, null, null, problemas, []);
        }

        return new Calculo(entrada, perfil, catalogos).Executar();
    }

    private sealed class Calculo(EntradaDeDimensionamento entrada, PerfilNormativo perfil, CatalogosDeProduto catalogos)
    {
        private readonly List<PassoDeCalculo> _passos = [];
        private decimal? _correnteDeProjeto;
        private int? _condutoresCarregados;
        private decimal? _fct;
        private decimal? _fca;
        private decimal? _secao;
        private decimal? _capacidade;
        private decimal? _disjuntor;
        private decimal? _queda;
        private string? _eletroduto;
        private decimal? _diametroInterno;
        private decimal? _ocupacao;
        private decimal? _secaoDoNeutro;
        private decimal? _secaoDeProtecao;
        private decimal? _idrNominal;
        private decimal? _idrSensibilidade;
        private bool _idrAvaliado;
        private readonly List<string> _avisos = [];

        public ResultadoDoDimensionamento Executar()
        {
            try
            {
                Dimensionar();
                return Resultado(SituacaoDoDimensionamento.Dimensionado, []);
            }
            catch (CalculoInterrompido interrupcao)
            {
                return Resultado(SituacaoDoDimensionamento.Interrompido, [interrupcao.Message]);
            }
        }

        private void Dimensionar()
        {
            var ib = CorrenteDeProjeto();
            var condutores = Consultar(perfil.CondutoresCarregados(entrada.Fases), "Condutores carregados",
                $"n = condutores carregados ({entrada.Fases})", [], "condutores");
            _condutoresCarregados = condutores;
            IsolacaoDoCondutor();
            ConstrucaoDoCondutor();
            var noSolo = perfil.Enterrado(entrada.MetodoDeInstalacao) ? " no solo" : string.Empty;
            _fct = Consultar(perfil.FatorDeTemperatura(entrada.MetodoDeInstalacao, entrada.Isolacao, entrada.TemperaturaAmbienteC), "Fator de correção de temperatura",
                $"FCT = tabela ({entrada.Isolacao}; {Numero(entrada.TemperaturaAmbienteC)} °C{noSolo})", [new ValorDoPasso("θ", entrada.TemperaturaAmbienteC, "°C")], string.Empty,
                entrada.OrigemDaTemperatura is { Length: > 0 } origemDaTemperatura ? $"θ: {origemDaTemperatura}" : null);
            _fca = Consultar(perfil.FatorDeAgrupamento(entrada.MetodoDeInstalacao, entrada.CircuitosAgrupados), "Fator de correção de agrupamento",
                $"FCA = tabela ({LinhaDoAgrupamento(entrada.CircuitosAgrupados)})", [new ValorDoPasso("circuitos", entrada.CircuitosAgrupados, string.Empty)], string.Empty,
                entrada.OrigemDoAgrupamento is { Length: > 0 } origemDoAgrupamento ? $"circuitos: {origemDoAgrupamento}" : null);

            var tipoDeCircuito = entrada.Tipo == TipoDeCarga.Iluminacao ? "Iluminacao" : "Forca";
            var secaoMinima = Consultar(perfil.SecaoMinimaMm2(tipoDeCircuito), "Seção mínima", $"Smín = tabela ({tipoDeCircuito})", [], "mm²");
            var secoes = Exigir(perfil.SecoesNominaisMm2(), "Seções nominais", "S ∈ seções nominais", "mm²");
            var piso = SecaoMinimaDoProjetista(secaoMinima, secoes);
            var candidatas = secoes.Where(secao => secao >= piso).ToList();

            var disjuntorDoProjetista = DisjuntorDoProjetista(ib);
            var indice = SecaoPelaCapacidade(ib, disjuntorDoProjetista, candidatas, piso, secoes);
            var disjuntores = Exigir(perfil.CorrentesNominaisDeDisjuntorA(), "Correntes nominais de disjuntor", "In ∈ correntes nominais", "A");
            var resistividade = Consultar(perfil.ResistividadeOhmMm2PorM(entrada.Material), "Resistividade do condutor",
                $"ρ = tabela ({entrada.Material})", [], "Ω·mm²/m");
            var limite = LimiteDeQueda();
            var (fator, correnteDaQueda, nomeDaCorrente) = BaseDaQueda(ib);

            var elevacoes = new List<string>();
            while (true)
            {
                var secao = candidatas[indice];
                var (capacidadeDeTabela, capacidade, referenciaDaCapacidade) = Capacidade(secao);
                decimal? disjuntor = disjuntorDoProjetista is { } fixo
                    ? (fixo <= capacidade ? fixo : null)
                    : disjuntores.Where(corrente => corrente >= ib && corrente <= capacidade).Select(corrente => (decimal?)corrente).FirstOrDefault();
                var queda = fator * resistividade * entrada.ComprimentoM * correnteDaQueda / (secao * entrada.TensaoV) * 100m;

                var motivo = disjuntor is null
                    ? disjuntorDoProjetista is { } exigido
                        ? $"IZ = {Numero(capacidade)} A abaixo do In = {Numero(exigido)} A do disjuntor do projetista"
                        : $"nenhum disjuntor entre IB = {Numero(ib)} A e IZ = {Numero(capacidade)} A"
                    : queda > limite
                        ? $"queda de tensão {Numero(queda)}% acima do limite de {Numero(limite)}%"
                        : null;

                if (motivo is null)
                {
                    Passo(referenciaDaCapacidade, "Capacidade de condução da seção adotada", "IZ = IZ₀(S) · FCA · FCT",
                        [new ValorDoPasso("S", secao, "mm²"), new ValorDoPasso("IZ₀", capacidadeDeTabela, "A"), new ValorDoPasso("FCA", _fca!.Value, string.Empty), new ValorDoPasso("FCT", _fct!.Value, string.Empty)],
                        capacidade, "A", elevacoes.Count > 0 ? string.Join("; ", elevacoes) : null);
                    Passo(perfil.ReferenciaDaRegra(RegraNormativa.CoordenacaoCondutorProtecao), "Disjuntor",
                        disjuntorDoProjetista is null ? "menor In com IB ≤ In ≤ IZ" : "In do projetista, com IB ≤ In ≤ IZ",
                        [new ValorDoPasso("IB", ib, "A"), new ValorDoPasso("IZ", capacidade, "A")], disjuntor, "A",
                        disjuntorDoProjetista is null
                            ? $"correntes nominais: {perfil.CorrentesNominaisDeDisjuntorA().Referencia}"
                            : $"decisão do projetista, verificada ({Justificativa()})");
                    Passo(perfil.ReferenciaDaRegra(RegraNormativa.QuedaDeTensao), "Queda de tensão", $"ΔV% = k · ρ · L · {nomeDaCorrente} / (S · V) · 100",
                        [new ValorDoPasso("k", fator, string.Empty), new ValorDoPasso("ρ", resistividade, "Ω·mm²/m"), new ValorDoPasso("L", entrada.ComprimentoM, "m"),
                         new ValorDoPasso(nomeDaCorrente, correnteDaQueda, "A"), new ValorDoPasso("S", secao, "mm²"), new ValorDoPasso("V", entrada.TensaoV, "V")],
                        queda, "%", $"fórmula resistiva (sem reatância), só {(entrada.Alimentador ? "o alimentador" : "o circuito terminal")}"
                                    + (entrada.OrigemDoComprimento is { Length: > 0 } origem ? $"; L: {origem}" : string.Empty));

                    _secao = secao;
                    _capacidade = capacidade;
                    _disjuntor = disjuntor;
                    _queda = queda;
                    Idr(disjuntor!.Value);
                    NeutroEProtecao(secao);
                    // C, E, F e G (sobre parede ou ao ar livre) não têm eletroduto: a memória termina no IDR.
                    if (perfil.ComEletroduto(entrada.MetodoDeInstalacao)) Eletroduto(secao);
                    return;
                }

                if (indice + 1 >= candidatas.Count)
                    Parar(referenciaDaCapacidade, "Seção do condutor", "S = próxima seção nominal", "mm²", $"nenhuma seção do perfil atende: {motivo}");

                elevacoes.Add($"seção elevada de {Numero(secao)} para {Numero(candidatas[indice + 1])} mm²: {motivo}");
                indice++;
            }
        }

        private decimal CorrenteDeProjeto()
        {
            var referencia = perfil.ReferenciaDaRegra(RegraNormativa.CorrenteDeProjeto);
            if (entrada.CorrenteDeProjeto is { } calculada)
            {
                if (entrada.Fases is not ("F+N" or "2F" or "3F" or "3F+N"))
                    Parar(referencia, "Corrente de projeto", "IB = ?", "A", $"configuração {entrada.Fases}: use F+N, 2F, 3F ou 3F+N");
                Passo(referencia, "Corrente de projeto", calculada.Expressao, calculada.Valores, calculada.CorrenteA, "A", calculada.Observacao);
                _correnteDeProjeto = calculada.CorrenteA;
                return calculada.CorrenteA;
            }

            var valores = new[] { new ValorDoPasso("S", entrada.PotenciaVA, "VA"), new ValorDoPasso("V", entrada.TensaoV, "V") };
            var (expressao, ib) = entrada.Fases switch
            {
                "F+N" or "2F" => ("IB = S / V", entrada.PotenciaVA / entrada.TensaoV),
                "3F" or "3F+N" => ("IB = S / (√3 · V)", entrada.PotenciaVA / (Raiz3 * entrada.TensaoV)),
                _ => Parar<(string, decimal)>(referencia, "Corrente de projeto", "IB = ?", "A",
                    $"configuração {entrada.Fases}: a divisão das cargas entre as fases não é modelada no MVP; use F+N ou 2F")
            };

            Passo(referencia, "Corrente de projeto", expressao, valores, ib, "A",
                entrada.OrigemDaPotencia is { Length: > 0 } origem ? $"S: {origem}" : null);
            _correnteDeProjeto = ib;
            return ib;
        }

        // A capacidade sai da isolação do circuito: o condutor escolhido no catálogo precisa ter essa isolação (ex.: cabo de
        // PVC com o circuito em EPR daria IZ acima da real).
        private void IsolacaoDoCondutor()
        {
            if (catalogos.Condutores.Isolacao(entrada.TipoDeCondutor) is not { } doCatalogo
                || string.Equals(doCatalogo.Isolacao, entrada.Isolacao.Trim(), StringComparison.OrdinalIgnoreCase)) return;

            Parar(doCatalogo.Referencia, "Isolação do condutor", $"isolação do circuito = a de '{entrada.TipoDeCondutor}'", string.Empty,
                $"o condutor '{entrada.TipoDeCondutor}' é de isolação {doCatalogo.Isolacao}, e o circuito está com {entrada.Isolacao}: " +
                "corrija AMP_MaterialIsolacao (ou a isolação padrão) ou o tipo de condutor");
        }

        // A construção do tipo de condutor (catálogo) precisa ser uma que o método admite (perfil): a capacidade de condução é
        // a da coluna do método. Admitida só sob condição que o modelo não mostra: segue, com aviso.
        private void ConstrucaoDoCondutor()
        {
            if (catalogos.Condutores.Construcao(entrada.TipoDeCondutor) is not { } doCatalogo
                || perfil.Construcao(entrada.MetodoDeInstalacao, doCatalogo.Construcao) is not { } admissao) return;

            if (!admissao.Admitida)
            {
                Parar(admissao.Referencia, "Construção do condutor", $"construção de '{entrada.TipoDeCondutor}' admitida no método {entrada.MetodoDeInstalacao}",
                    string.Empty,
                    $"o condutor '{entrada.TipoDeCondutor}' é {doCatalogo.Construcao}, e o método {entrada.MetodoDeInstalacao} pede " +
                    $"{string.Join(" ou ", admissao.Admitidas)}: corrija AMP_MetodoInstalacao (ou o método padrão) ou o tipo de condutor");
            }

            if (admissao.Condicao is { } condicao)
                _avisos.Add($"{doCatalogo.Construcao} no método {entrada.MetodoDeInstalacao}: {condicao}");
        }

        // k e corrente da queda: os da entrada (com a conta na memória) ou, sem eles, k pela configuração e IB.
        private (decimal Fator, decimal Corrente, string Nome) BaseDaQueda(decimal ib)
        {
            if (entrada.CorrenteDaQueda is not { } informada)
                return (entrada.Fases is "3F" or "3F+N" ? Raiz3 : 2m, ib, "IB");

            Passo(informada.Referencia, "Corrente para a queda de tensão", informada.Expressao, informada.Valores, informada.CorrenteA, "A", informada.Observacao);
            return (informada.Fator, informada.CorrenteA, "IΔV");
        }

        private decimal LimiteDeQueda()
        {
            if (entrada.LimiteDeQueda is not { } limite)
                return Consultar(perfil.QuedaDeTensaoMaximaPct("circuito_terminal"), "Limite de queda de tensão", "ΔV%máx = tabela (circuito terminal)", [], "%");

            Passo(limite.Referencia, "Limite de queda de tensão", limite.Expressao, limite.Valores, limite.ValorPct, "%", limite.Observacao);
            return limite.ValorPct;
        }

        private void Idr(decimal disjuntor)
        {
            var tabela = perfil.ProtecaoDiferencialPorLocal();
            var avaliacao = entrada.Alimentador
                ? new AvaliacaoPelaTabela("alimentador de quadro: a tabela por local vale para os circuitos terminais", [])
                : AvaliarPelaTabela(tabela);
            var sensibilidade = entrada.IdrDoProjetista is { } decisao
                ? PelaDecisaoDoProjetista(decisao, tabela.Referencia, avaliacao)
                : entrada.Alimentador
                    ? SemIdrNoAlimentador(tabela.Referencia)
                    : PelaTabela(tabela.Referencia, avaliacao);
            if (sensibilidade is null)
            {
                _idrAvaliado = true;
                return;
            }
            _idrSensibilidade = sensibilidade;

            const string Criterio = "menor In(IDR) ≥ In(disjuntor)";
            var referencia = perfil.ReferenciaDaRegra(RegraNormativa.CoordenacaoIdrDisjuntor);
            var dadoDasCorrentes = perfil.CorrentesNominaisDeIdrA();
            var correntes = Exigir(dadoDasCorrentes, "Correntes nominais de IDR", "In(IDR) ∈ correntes nominais", "A");
            decimal? nominal = correntes.Where(corrente => corrente >= disjuntor).Select(corrente => (decimal?)corrente).FirstOrDefault();
            if (nominal is null)
            {
                Parar(referencia, "Corrente nominal do IDR", Criterio, "A",
                    $"nenhuma corrente nominal de IDR do perfil atende In = {Numero(disjuntor)} A (maior: {Numero(correntes[^1])} A)");
            }

            Passo(referencia, "Corrente nominal do IDR", Criterio, [new ValorDoPasso("In", disjuntor, "A")], nominal, "A",
                $"correntes nominais de IDR: {dadoDasCorrentes.Referencia}");
            _idrNominal = nominal;
            _idrAvaliado = true;
        }

        private AvaliacaoPelaTabela AvaliarPelaTabela(DadoNormativo<IReadOnlyDictionary<string, ProtecaoDiferencialDoLocal>> tabela)
        {
            if (!tabela.Disponivel) return new AvaliacaoPelaTabela(tabela.Ausencia, []);

            var semLocal = entrada.LocaisDosPontos.Count(string.IsNullOrWhiteSpace);
            if (semLocal > 0) return new AvaliacaoPelaTabela($"{Pontos(semLocal)} sem local: informe o local ou a decisão do projetista sobre o IDR", []);

            var locais = entrada.LocaisDosPontos
                .GroupBy(local => local!.Trim(), StringComparer.Ordinal)
                .OrderBy(grupo => grupo.Key, StringComparer.Ordinal)
                .ToList();
            var foraDaTabela = locais.Where(grupo => !tabela.Valor.ContainsKey(grupo.Key)).Select(grupo => $"'{grupo.Key}'").ToList();
            if (foraDaTabela.Count > 0) return new AvaliacaoPelaTabela($"local fora da tabela de IDR: {string.Join(", ", foraDaTabela)}", []);

            return new AvaliacaoPelaTabela(null, locais
                .Select(grupo => new ExigenciaDoLocal(grupo.Key, grupo.Count(), tabela.Valor[grupo.Key].SensibilidadeExigidaMa(entrada.Tipo)))
                .ToList());
        }

        private decimal? PelaTabela(string referencia, AvaliacaoPelaTabela avaliacao)
        {
            var expressao = $"n = pontos em locais que exigem IDR para {CodigosDeTipoDeCarga.Codigo(entrada.Tipo)}";
            if (avaliacao.Impedimento is not null) Parar(referencia, "Exigência de IDR", expressao, "pontos", avaliacao.Impedimento);

            Passo(referencia, "Exigência de IDR", expressao, [new ValorDoPasso("total", entrada.LocaisDosPontos.Count, "pontos")],
                avaliacao.PontosQueExigem, "pontos", avaliacao.Descrever());
            if (avaliacao.SensibilidadeExigida is not { } sensibilidade) return null;

            Passo(referencia, "Sensibilidade do IDR", "IΔn = menor IΔn máx dos locais que exigem IDR", [], sensibilidade, "mA");
            return sensibilidade;
        }

        // Sem decisão do projetista, o alimentador não leva IDR: a exigência por local é dos circuitos terminais.
        private decimal? SemIdrNoAlimentador(string referencia)
        {
            Passo(referencia, "Exigência de IDR", "sem IDR no alimentador (sem decisão do projetista)", [], 0m, "IDR",
                "a tabela de IDR por local vale para os circuitos terminais; IDR no alimentador só por decisão do projetista (AMP_IDR_DecisaoProjetista)");
            return null;
        }

        private decimal? PelaDecisaoDoProjetista(DecisaoDeIdr decisao, string referencia, AvaliacaoPelaTabela avaliacao)
        {
            var total = entrada.LocaisDosPontos.Count;
            var motivo = string.IsNullOrWhiteSpace(decisao.Motivo) ? "sem motivo informado" : $"motivo: {decisao.Motivo.Trim()}";
            var observacao = $"decisão do projetista, prevalece sobre a tabela ({motivo}); pela tabela: {avaliacao.Descrever()}";
            // O alimentador não tem pontos: a decisão vale para ele inteiro.
            ValorDoPasso[] valores = entrada.Alimentador ? [] : [new ValorDoPasso("total", total, "pontos")];

            if (!decisao.Exigir)
            {
                var exigidos = avaliacao.PontosQueExigem;
                Passo(referencia, "Exigência de IDR", entrada.Alimentador ? "IDR no alimentador dispensado pelo projetista" : "n = 0 (IDR dispensado pelo projetista)",
                    valores, 0m, entrada.Alimentador ? "IDR" : "pontos",
                    exigidos > 0 ? $"{observacao}; ATENÇÃO: a tabela exige IDR em {Pontos(exigidos)}" : observacao);
                if (exigidos > 0) _avisos.Add($"IDR dispensado pelo projetista, mas a tabela o exige em {Pontos(exigidos)}");
                return null;
            }

            if (entrada.Alimentador) Passo(referencia, "Exigência de IDR", "IDR no alimentador (exigido pelo projetista)", valores, 1m, "IDR", observacao);
            else Passo(referencia, "Exigência de IDR", "n = todos os pontos (IDR exigido pelo projetista)", valores, total, "pontos", observacao);
            var sensibilidade = decisao.SensibilidadeMa!.Value;
            var dadoDaSerie = perfil.SensibilidadesNominaisDeIdrMa();
            var serie = Exigir(dadoDaSerie, "Sensibilidades nominais de IDR", "IΔn ∈ sensibilidades nominais", "mA");
            if (!serie.Contains(sensibilidade))
            {
                Parar(dadoDaSerie.Referencia, "Sensibilidade do IDR", "IΔn = informada pelo projetista", "mA",
                    $"IΔn = {Numero(sensibilidade)} mA do projetista fora das sensibilidades nominais do perfil ({string.Join("; ", serie.Select(Numero))})");
            }

            string? alerta = null;
            if (avaliacao.SensibilidadeExigida is { } exigida && sensibilidade > exigida)
            {
                alerta = $"IΔn de {Numero(sensibilidade)} mA acima da máxima de {Numero(exigida)} mA exigida pela tabela";
                _avisos.Add(alerta);
            }

            Passo(referencia, "Sensibilidade do IDR", "IΔn = informada pelo projetista", [], sensibilidade, "mA",
                string.Join("; ", new[] { alerta, $"sensibilidades nominais: {dadoDaSerie.Referencia}" }.OfType<string>()));
            return sensibilidade;
        }

        private void NeutroEProtecao(decimal secao)
        {
            if (entrada.Fases.EndsWith("+N", StringComparison.Ordinal))
            {
                Passo(perfil.ReferenciaDaRegra(RegraNormativa.SecaoDoNeutro), "Seção do neutro", "SN = S", [new ValorDoPasso("S", secao, "mm²")], secao, "mm²",
                    entrada.Fases == "F+N"
                        ? null
                        : "premissa: sem harmônicas significativas, como nos condutores carregados; a seção reduzida permitida acima de 25 mm² não é aplicada");
                _secaoDoNeutro = secao;
            }

            _secaoDeProtecao = Consultar(perfil.SecaoDoCondutorDeProtecaoMm2(secao), "Seção do condutor de proteção", "SPE = tabela (S)",
                [new ValorDoPasso("S", secao, "mm²")], "mm²", "condutor de proteção do mesmo material das fases");

            // Sem eletroduto e fora de cabo multipolar, o PE não está no mesmo cabo nem no mesmo conduto das fases: tem mínimo.
            if (perfil.ComEletroduto(entrada.MetodoDeInstalacao)) return;
            var construcao = catalogos.Condutores.Construcao(entrada.TipoDeCondutor)?.Construcao;
            if (construcao == ConstrucoesDeCondutor.CaboMultipolar || (construcao is null && perfil.SoCaboMultipolar(entrada.MetodoDeInstalacao))) return;
            if (perfil.SecaoMinimaDoPeForaDoCaboMm2(entrada.Material) is not { } dadoDoMinimo) return;

            var minimo = Consultar(dadoDoMinimo, "Seção mínima do condutor de proteção fora do cabo", $"SPE,mín = tabela ({entrada.Material})", [], "mm²",
                $"método {entrada.MetodoDeInstalacao}, sem eletroduto, com {construcao ?? "construção do condutor não informada"}: " +
                "o condutor de proteção não vai no mesmo cabo nem no mesmo conduto das fases");
            if (minimo <= _secaoDeProtecao) return;

            Passo(dadoDoMinimo.Referencia, "Seção do condutor de proteção adotada", "SPE = máx(SPE(S); SPE,mín)",
                [new ValorDoPasso("SPE(S)", _secaoDeProtecao!.Value, "mm²"), new ValorDoPasso("SPE,mín", minimo, "mm²")], minimo, "mm²");
            _secaoDeProtecao = minimo;
        }

        private void Eletroduto(decimal secao)
        {
            var referenciaDaRegra = perfil.ReferenciaDaRegra(RegraNormativa.CondutoresNoEletroduto);
            var (composicao, condutores) = entrada.Fases switch
            {
                "F+N" => ("F + N + PE", 3),
                "2F" => ("2F + PE", 3),
                "3F" => ("3F + PE", 4),
                "3F+N" => ("3F + N + PE", 5),
                _ => Parar<(string, int)>(referenciaDaRegra, "Condutores no eletroduto", "n = ?", "condutores",
                    $"configuração {entrada.Fases} sem contagem de condutores no eletroduto")
            };
            Passo(referenciaDaRegra, "Condutores no eletroduto", $"n = {composicao}", [], condutores, "condutores",
                (entrada.Fases.EndsWith("+N", StringComparison.Ordinal) ? "neutro e proteção" : "proteção")
                + " com o diâmetro da fase (conservador para a ocupação)");

            var diametro = Consultar(catalogos.Condutores.DiametroExternoMm(entrada.TipoDeCondutor, secao), "Diâmetro externo do condutor",
                $"d = catálogo ({Tipo(entrada.TipoDeCondutor)}; {Numero(secao)} mm²)", [], "mm",
                catalogos.Condutores.Isolacao(entrada.TipoDeCondutor) is { } isolacao ? $"isolação do condutor: {isolacao.Isolacao}, a do circuito" : null);
            var dadoDaTaxa = perfil.OcupacaoMaximaDeEletrodutoPct(condutores);
            var faixa = perfil.FaixaDeOcupacao(condutores);
            var linhaDaTabela = faixa is { } usada && usada != condutores ? $"{condutores} condutores: faixa de {usada} ou mais" : $"{condutores} condutores";
            var taxa = Consultar(dadoDaTaxa, "Taxa máxima de ocupação", $"taxa = tabela ({linhaDaTabela})", [], "%");
            var dadoDosTamanhos = catalogos.Eletrodutos.Tamanhos(entrada.TipoDeEletroduto);
            var tamanhos = Exigir(dadoDosTamanhos, "Tamanhos de eletroduto", $"Di ∈ catálogo ({Tipo(entrada.TipoDeEletroduto)})", "mm");

            decimal Ocupacao(TamanhoDeEletroduto tamanho) =>
                condutores * diametro * diametro * 100m / (tamanho.DiametroInternoMm * tamanho.DiametroInternoMm);
            string Descrever(TamanhoDeEletroduto tamanho) =>
                $"{tamanho.Nominal} ({Numero(Ocupacao(tamanho))}%)";

            const string Criterio = "menor Di com n · d² / Di² · 100 ≤ taxa";
            var adotado = tamanhos.FirstOrDefault(tamanho => Ocupacao(tamanho) <= taxa);
            if (adotado is null)
            {
                Parar(dadoDosTamanhos.Referencia, "Eletroduto adotado", Criterio, "mm",
                    $"nenhum eletroduto '{entrada.TipoDeEletroduto}' do catálogo atende: taxa máxima de {Numero(taxa)}%, e o maior tamanho fica em {Descrever(tamanhos[^1])}");
            }

            var recusados = tamanhos.TakeWhile(tamanho => tamanho != adotado).Select(Descrever).ToList();
            Passo(dadoDosTamanhos.Referencia, "Eletroduto adotado", Criterio,
                [new ValorDoPasso("n", condutores, "condutores"), new ValorDoPasso("d", diametro, "mm"), new ValorDoPasso("taxa", taxa, "%")],
                adotado!.DiametroInternoMm, "mm",
                $"tamanho nominal {adotado.Nominal} ({entrada.TipoDeEletroduto})" + (recusados.Count > 0 ? $"; acima da taxa: {string.Join(", ", recusados)}" : string.Empty));

            var ocupacao = Ocupacao(adotado);
            Passo(dadoDaTaxa.Referencia, "Ocupação do eletroduto", "ocupação = n · d² / Di² · 100",
                [new ValorDoPasso("n", condutores, "condutores"), new ValorDoPasso("d", diametro, "mm"), new ValorDoPasso("Di", adotado.DiametroInternoMm, "mm")],
                ocupacao, "%", "só os condutores deste circuito: outros circuitos na mesma tubulação não entram");

            _eletroduto = adotado.Nominal;
            _diametroInterno = adotado.DiametroInternoMm;
            _ocupacao = ocupacao;
        }

        /// <summary>Piso da seção: a mínima da norma ou, se maior, a do projetista (que precisa ser seção nominal).</summary>
        private decimal SecaoMinimaDoProjetista(decimal secaoMinima, IReadOnlyList<decimal> secoes)
        {
            if (entrada.SecaoMinimaDoProjetistaMm2 is not { } doProjetista) return secaoMinima;

            const string Descricao = "Seção mínima do projetista";
            const string Expressao = "S ≥ S do projetista";
            var referencia = perfil.SecoesNominaisMm2().Referencia;
            if (!secoes.Contains(doProjetista))
                Parar(referencia, Descricao, Expressao, "mm²", $"{Numero(doProjetista)} mm² não é seção nominal do perfil ({string.Join("; ", secoes.Select(Numero))})");

            if (doProjetista < secaoMinima)
            {
                Passo(referencia, Descricao, Expressao, [], doProjetista, "mm²",
                    $"decisão do projetista ({Justificativa()}); abaixo da seção mínima da norma ({Numero(secaoMinima)} mm²), que prevalece");
                _avisos.Add($"seção mínima do projetista ({Numero(doProjetista)} mm²) abaixo da mínima da norma ({Numero(secaoMinima)} mm²): vale a da norma");
                return secaoMinima;
            }

            Passo(referencia, Descricao, Expressao, [], doProjetista, "mm²",
                $"decisão do projetista ({Justificativa()}); o cálculo pode adotar seção maior, nunca menor");
            return doProjetista;
        }

        /// <summary>In do projetista, se houver: da série do perfil e não abaixo de IB.</summary>
        private decimal? DisjuntorDoProjetista(decimal ib)
        {
            if (entrada.DisjuntorDoProjetistaA is not { } doProjetista) return null;

            const string Descricao = "Disjuntor do projetista";
            var referencia = perfil.ReferenciaDaRegra(RegraNormativa.CoordenacaoCondutorProtecao);
            var serie = Exigir(perfil.CorrentesNominaisDeDisjuntorA(), "Correntes nominais de disjuntor", "In ∈ correntes nominais", "A");
            if (!serie.Contains(doProjetista))
                Parar(referencia, Descricao, "In ∈ correntes nominais", "A", $"In = {Numero(doProjetista)} A fora das correntes nominais do perfil ({string.Join("; ", serie.Select(Numero))})");
            if (doProjetista < ib)
                Parar(referencia, Descricao, "IB ≤ In", "A", $"In = {Numero(doProjetista)} A do projetista abaixo de IB = {Numero(ib)} A");

            Passo(referencia, Descricao, "IB ≤ In (IZ ≥ In verificado na seção)", [new ValorDoPasso("IB", ib, "A")], doProjetista, "A",
                $"decisão do projetista ({Justificativa()}); a seção sobe até IZ ≥ In");
            return doProjetista;
        }

        private int SecaoPelaCapacidade(decimal ib, decimal? disjuntorDoProjetista, List<decimal> candidatas, decimal secaoMinima, IReadOnlyList<decimal> secoes)
        {
            // Com disjuntor do projetista, a seção precisa de IZ ≥ In (e In ≥ IB já foi verificado).
            var alvo = disjuntorDoProjetista ?? ib;
            // Com seção do projetista, o piso não é mais a Smín da tabela: outro nome, para a memória não confundir.
            var piso = entrada.SecaoMinimaDoProjetistaMm2 is null ? "Smín" : "Spiso";
            var (expressao, nome) = disjuntorDoProjetista is null
                ? ($"menor S ≥ {piso} com IZ₀(S) · FCA · FCT ≥ IB", "IB")
                : ($"menor S ≥ {piso} com IZ₀(S) · FCA · FCT ≥ In do projetista", "In");
            var referencia = PerfilNormativo.TodoNorma;
            for (var indice = 0; indice < candidatas.Count; indice++)
            {
                var (_, capacidade, referenciaDaTabela) = Capacidade(candidatas[indice]);
                referencia = referenciaDaTabela;
                if (capacidade < alvo) continue;

                Passo(referencia, "Seção pela capacidade de condução", expressao,
                    [new ValorDoPasso(piso, secaoMinima, "mm²"), new ValorDoPasso(nome, alvo, "A"), new ValorDoPasso("FCA", _fca!.Value, string.Empty), new ValorDoPasso("FCT", _fct!.Value, string.Empty)],
                    candidatas[indice], "mm²", $"seções nominais: {perfil.SecoesNominaisMm2().Referencia}");
                return indice;
            }

            var maior = candidatas.Count > 0 ? candidatas[^1] : (secoes.Count > 0 ? secoes[^1] : 0m);
            return Parar<int>(referencia, "Seção pela capacidade de condução", expressao, "mm²",
                $"nenhuma seção do perfil atende {nome} = {Numero(alvo)} A (maior seção: {Numero(maior)} mm²)");
        }

        // Faixa da tabela quando ela cobre mais de um número de circuitos (ex.: "13 circuitos: faixa de 12 a 15").
        private string LinhaDoAgrupamento(int circuitos)
        {
            var contagem = Contagem(circuitos, "circuito", "circuitos");
            return perfil.FaixaDeAgrupamento(entrada.MetodoDeInstalacao, circuitos) switch
            {
                { Fim: null } faixa => $"{contagem}: faixa de {faixa.Inicio} ou mais",
                { } faixa when faixa.Fim > faixa.Inicio => $"{contagem}: faixa de {faixa.Inicio} a {faixa.Fim}",
                _ => contagem
            };
        }

        private string Justificativa() =>
            string.IsNullOrWhiteSpace(entrada.Justificativa) ? "sem justificativa informada" : $"justificativa: {entrada.Justificativa.Trim()}";

        private (decimal DeTabela, decimal Corrigida, string Referencia) Capacidade(decimal secao)
        {
            var dado = perfil.CapacidadeDeConducaoA(entrada.MetodoDeInstalacao, entrada.Isolacao, entrada.Material, _condutoresCarregados!.Value, secao);
            if (!dado.Disponivel)
            {
                Parar(dado.Referencia, "Capacidade de condução",
                    $"IZ₀ = tabela ({entrada.MetodoDeInstalacao}; {entrada.Isolacao}; {entrada.Material}; {_condutoresCarregados} condutores; {Numero(secao)} mm²)",
                    "A", dado.Ausencia!);
            }

            return (dado.Valor, dado.Valor * _fca!.Value * _fct!.Value, dado.Referencia);
        }

        private decimal Consultar(DadoNormativo<decimal> dado, string descricao, string expressao, IReadOnlyList<ValorDoPasso> valores, string unidade,
            string? observacao = null)
        {
            if (!dado.Disponivel) Parar(dado.Referencia, descricao, expressao, unidade, dado.Ausencia!);
            Passo(dado.Referencia, descricao, expressao, valores, dado.Valor, unidade, observacao);
            return dado.Valor;
        }

        private int Consultar(DadoNormativo<int> dado, string descricao, string expressao, IReadOnlyList<ValorDoPasso> valores, string unidade)
        {
            if (!dado.Disponivel) Parar(dado.Referencia, descricao, expressao, unidade, dado.Ausencia!);
            Passo(dado.Referencia, descricao, expressao, valores, dado.Valor, unidade);
            return dado.Valor;
        }

        private T Exigir<T>(DadoNormativo<T> dado, string descricao, string expressao, string unidade)
        {
            if (!dado.Disponivel) Parar(dado.Referencia, descricao, expressao, unidade, dado.Ausencia!);
            return dado.Valor;
        }

        private void Passo(string referencia, string descricao, string expressao, IReadOnlyList<ValorDoPasso> valores, decimal? resultado, string unidade, string? observacao = null) =>
            _passos.Add(new PassoDeCalculo(referencia, descricao, expressao, valores, resultado, unidade, observacao));

        private void Parar(string referencia, string descricao, string expressao, string unidade, string motivo) =>
            Parar<bool>(referencia, descricao, expressao, unidade, motivo);

        private T Parar<T>(string referencia, string descricao, string expressao, string unidade, string motivo)
        {
            Passo(referencia, descricao, expressao, [], null, unidade, motivo);
            throw new CalculoInterrompido(motivo);
        }

        private ResultadoDoDimensionamento Resultado(SituacaoDoDimensionamento situacao, IReadOnlyList<string> problemas) =>
            new(entrada.Circuito, situacao, perfil.Nome, _correnteDeProjeto, _condutoresCarregados, _fct, _fca, _secao, _capacidade, _disjuntor,
                _idrNominal, _idrSensibilidade, _queda, _eletroduto, _diametroInterno, _ocupacao,
                new MemoriaDeCalculo(entrada.Circuito, perfil.Nome, _passos), problemas, _avisos, _idrAvaliado, _secaoDoNeutro, _secaoDeProtecao);

        private static string Numero(decimal valor) => NumeroEmTexto.FormatarParaLeitura(valor);

        private static string Tipo(string? tipo) => string.IsNullOrWhiteSpace(tipo) ? "tipo não informado" : tipo;

        private static string Pontos(int quantidade) => Contagem(quantidade, "ponto", "pontos");

        private static string Contagem(int quantidade, string singular, string plural) => $"{quantidade} {(quantidade == 1 ? singular : plural)}";

        private sealed record ExigenciaDoLocal(string Local, int Pontos, decimal? SensibilidadeMa);

        /// <summary>O que a tabela de proteção diferencial diz para o circuito, ou por que não dá para avaliar.</summary>
        private sealed record AvaliacaoPelaTabela(string? Impedimento, IReadOnlyList<ExigenciaDoLocal> PorLocal)
        {
            public int PontosQueExigem => PorLocal.Where(local => local.SensibilidadeMa is not null).Sum(local => local.Pontos);

            public decimal? SensibilidadeExigida => PorLocal.Select(local => local.SensibilidadeMa).Min();

            public string Descrever() => Impedimento is not null
                ? $"não avaliada ({Impedimento})"
                : string.Join("; ", PorLocal.Select(local => $"{local.Local} ({Calculo.Pontos(local.Pontos)}): " +
                    (local.SensibilidadeMa is { } sensibilidade ? $"exige IΔn ≤ {Numero(sensibilidade)} mA" : "não exige")));
        }
    }

    private sealed class CalculoInterrompido(string motivo) : Exception(motivo);
}
