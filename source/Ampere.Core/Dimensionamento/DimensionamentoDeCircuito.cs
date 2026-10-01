using Ampere.Core.Cargas;
using Ampere.Core.Catalogos;
using Ampere.Core.Memoria;
using Ampere.Core.Normas;

namespace Ampere.Core.Dimensionamento;

/// <summary>
///     Motor de dimensionamento de um circuito terminal: IB → condutores carregados → FCT → FCA → seção mínima →
///     seção pela capacidade de condução → disjuntor (IB ≤ In ≤ IZ) → queda de tensão → IDR → eletroduto (ocupação).
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
///         fabricante; catálogo sem dados interrompe o cálculo como tabela TODO_NORMA.</item>
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
            _fct = Consultar(perfil.FatorDeTemperatura(entrada.MetodoDeInstalacao, entrada.Isolacao, entrada.TemperaturaAmbienteC), "Fator de correção de temperatura",
                $"FCT = tabela ({entrada.Isolacao}; {Numero(entrada.TemperaturaAmbienteC)} °C)", [new ValorDoPasso("θ", entrada.TemperaturaAmbienteC, "°C")], string.Empty,
                entrada.OrigemDaTemperatura is { Length: > 0 } origemDaTemperatura ? $"θ: {origemDaTemperatura}" : null);
            _fca = Consultar(perfil.FatorDeAgrupamento(entrada.CircuitosAgrupados), "Fator de correção de agrupamento",
                $"FCA = tabela ({Contagem(entrada.CircuitosAgrupados, "circuito", "circuitos")})", [new ValorDoPasso("circuitos", entrada.CircuitosAgrupados, string.Empty)], string.Empty,
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
            var limite = Consultar(perfil.QuedaDeTensaoMaximaPct("circuito_terminal"), "Limite de queda de tensão",
                "ΔV%máx = tabela (circuito terminal)", [], "%");

            var fator = entrada.Fases is "3F" or "3F+N" ? Raiz3 : 2m;
            var elevacoes = new List<string>();
            while (true)
            {
                var secao = candidatas[indice];
                var (capacidadeDeTabela, capacidade, referenciaDaCapacidade) = Capacidade(secao);
                decimal? disjuntor = disjuntorDoProjetista is { } fixo
                    ? (fixo <= capacidade ? fixo : null)
                    : disjuntores.Where(corrente => corrente >= ib && corrente <= capacidade).Select(corrente => (decimal?)corrente).FirstOrDefault();
                var queda = fator * resistividade * entrada.ComprimentoM * ib / (secao * entrada.TensaoV) * 100m;

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
                    Passo(perfil.ReferenciaDaRegra(RegraNormativa.QuedaDeTensao), "Queda de tensão", "ΔV% = k · ρ · L · IB / (S · V) · 100",
                        [new ValorDoPasso("k", fator, string.Empty), new ValorDoPasso("ρ", resistividade, "Ω·mm²/m"), new ValorDoPasso("L", entrada.ComprimentoM, "m"),
                         new ValorDoPasso("IB", ib, "A"), new ValorDoPasso("S", secao, "mm²"), new ValorDoPasso("V", entrada.TensaoV, "V")],
                        queda, "%", "fórmula resistiva (sem reatância), só o circuito terminal"
                                    + (entrada.OrigemDoComprimento is { Length: > 0 } origem ? $"; L: {origem}" : string.Empty));

                    _secao = secao;
                    _capacidade = capacidade;
                    _disjuntor = disjuntor;
                    _queda = queda;
                    Idr(disjuntor!.Value);
                    Eletroduto(secao);
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
            var valores = new[] { new ValorDoPasso("S", entrada.PotenciaVA, "VA"), new ValorDoPasso("V", entrada.TensaoV, "V") };
            var (expressao, ib) = entrada.Fases switch
            {
                "F+N" or "2F" => ("IB = S / V", entrada.PotenciaVA / entrada.TensaoV),
                "3F" or "3F+N" => ("IB = S / (√3 · V)", entrada.PotenciaVA / (Raiz3 * entrada.TensaoV)),
                _ => Parar<(string, decimal)>(referencia, "Corrente de projeto", "IB = ?", "A",
                    $"configuração {entrada.Fases}: a divisão das cargas entre as fases não é modelada no MVP; use F+N ou 2F")
            };

            Passo(referencia, "Corrente de projeto", expressao, valores, ib, "A");
            _correnteDeProjeto = ib;
            return ib;
        }

        private void Idr(decimal disjuntor)
        {
            var tabela = perfil.ProtecaoDiferencialPorLocal();
            var avaliacao = AvaliarPelaTabela(tabela);
            var sensibilidade = entrada.IdrDoProjetista is { } decisao
                ? PelaDecisaoDoProjetista(decisao, tabela.Referencia, avaliacao)
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

        private decimal? PelaDecisaoDoProjetista(DecisaoDeIdr decisao, string referencia, AvaliacaoPelaTabela avaliacao)
        {
            var total = entrada.LocaisDosPontos.Count;
            var motivo = string.IsNullOrWhiteSpace(decisao.Motivo) ? "sem motivo informado" : $"motivo: {decisao.Motivo.Trim()}";
            var observacao = $"decisão do projetista, prevalece sobre a tabela ({motivo}); pela tabela: {avaliacao.Descrever()}";
            ValorDoPasso[] valores = [new ValorDoPasso("total", total, "pontos")];

            if (!decisao.Exigir)
            {
                var exigidos = avaliacao.PontosQueExigem;
                Passo(referencia, "Exigência de IDR", "n = 0 (IDR dispensado pelo projetista)", valores, 0m, "pontos",
                    exigidos > 0 ? $"{observacao}; ATENÇÃO: a tabela exige IDR em {Pontos(exigidos)}" : observacao);
                if (exigidos > 0) _avisos.Add($"IDR dispensado pelo projetista, mas a tabela o exige em {Pontos(exigidos)}");
                return null;
            }

            Passo(referencia, "Exigência de IDR", "n = todos os pontos (IDR exigido pelo projetista)", valores, total, "pontos", observacao);
            var sensibilidade = decisao.SensibilidadeMa!.Value;
            string? alerta = null;
            if (avaliacao.SensibilidadeExigida is { } exigida && sensibilidade > exigida)
            {
                alerta = $"IΔn de {Numero(sensibilidade)} mA acima da máxima de {Numero(exigida)} mA exigida pela tabela";
                _avisos.Add(alerta);
            }

            Passo(referencia, "Sensibilidade do IDR", "IΔn = informada pelo projetista", [], sensibilidade, "mA", alerta);
            return sensibilidade;
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
                $"d = catálogo ({Tipo(entrada.TipoDeCondutor)}; {Numero(secao)} mm²)", [], "mm");
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
            var (expressao, nome) = disjuntorDoProjetista is null
                ? ("menor S ≥ Smín com IZ₀(S) · FCA · FCT ≥ IB", "IB")
                : ("menor S ≥ Smín com IZ₀(S) · FCA · FCT ≥ In do projetista", "In");
            var referencia = PerfilNormativo.TodoNorma;
            for (var indice = 0; indice < candidatas.Count; indice++)
            {
                var (_, capacidade, referenciaDaTabela) = Capacidade(candidatas[indice]);
                referencia = referenciaDaTabela;
                if (capacidade < alvo) continue;

                Passo(referencia, "Seção pela capacidade de condução", expressao,
                    [new ValorDoPasso("Smín", secaoMinima, "mm²"), new ValorDoPasso(nome, alvo, "A"), new ValorDoPasso("FCA", _fca!.Value, string.Empty), new ValorDoPasso("FCT", _fct!.Value, string.Empty)],
                    candidatas[indice], "mm²", $"seções nominais: {perfil.SecoesNominaisMm2().Referencia}");
                return indice;
            }

            var maior = candidatas.Count > 0 ? candidatas[^1] : (secoes.Count > 0 ? secoes[^1] : 0m);
            return Parar<int>(referencia, "Seção pela capacidade de condução", expressao, "mm²",
                $"nenhuma seção do perfil atende {nome} = {Numero(alvo)} A (maior seção: {Numero(maior)} mm²)");
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
                new MemoriaDeCalculo(entrada.Circuito, perfil.Nome, _passos), problemas, _avisos, _idrAvaliado);

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
