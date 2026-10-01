using Ampere.Core.Cargas;
using Ampere.Core.Alimentadores;
using Ampere.Core.Catalogos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;
using Ampere.Core.Quadros;

namespace Ampere.Core.Verificacao;

/// <summary>Ponto de carga do documento (família com conector elétrico de força), como o adapter o lê.</summary>
/// <param name="TipoDeCarga">AMP_TipoCarga (texto; nulo = vazio).</param>
/// <param name="Local">AMP_Local (nulo = vazio).</param>
/// <param name="Circuito">Circuito de força de que o ponto é membro (nulo = fora de circuito).</param>
/// <param name="Aparelho">AMP_Aparelho (nulo = vazio): a demanda da entrada precisa dele nos TUE.</param>
public sealed record PontoVerificado(long Id, string? TipoDeCarga, string? Local, long? Circuito, string? Aparelho = null);

/// <summary>Circuito de força do documento, do Ampere ou não.</summary>
/// <param name="Numero">AMP_NumeroCircuito (nulo = circuito criado fora do Ampere).</param>
/// <param name="Nome">Nome do circuito no Revit, para identificar o que não é do Ampere.</param>
/// <param name="MemoriaGravada">AMP_MemoriaCalculoId do circuito (nulo = nunca dimensionado ou apagado).</param>
public sealed record CircuitoVerificado(long Id, string? Numero, string? Quadro, string Nome, string? MemoriaGravada);

/// <summary>Porta de leitura da verificação: nada é gravado.</summary>
public interface IDocumentoDeVerificacao
{
    IReadOnlyList<PontoVerificado> LerPontos();

    IReadOnlyList<CircuitoVerificado> LerCircuitosDeForca();

    /// <summary>Os mesmos dados que o dimensionamento lê, para refazer o cálculo e comparar com a memória gravada.</summary>
    IReadOnlyList<DadosDoCircuito> LerCircuitos(IReadOnlyCollection<long> ids);

    /// <summary>Condições do projeto, guardadas pela última rodada completa do dimensionamento (nulas se nunca guardadas).</summary>
    CondicoesDoProjeto? LerCondicoes();

    /// <summary>Condições da rodada que dimensionou cada circuito (só os que as têm guardadas).</summary>
    IReadOnlyDictionary<long, CondicoesDoProjeto> LerCondicoesDosCircuitos(IReadOnlyCollection<long> ids);

    /// <summary>Os AMP_* de resultado como estão no modelo, para achar resultado editado à mão.</summary>
    IReadOnlyDictionary<long, ResultadosNoCircuito> LerResultados(IReadOnlyCollection<long> ids);
}

/// <summary>Quão grave é a pendência.</summary>
public enum GravidadeDaPendencia
{
    /// <summary>Impede o fluxo do Ampere (a carga fica fora do quadro, o circuito não é calculado).</summary>
    Erro,

    /// <summary>O modelo não mostra o resultado atual, ou há decisão contrária à norma.</summary>
    Aviso,

    /// <summary>Para conhecimento (ex.: onde o cálculo para por falta de dado de catálogo).</summary>
    Informacao
}

/// <summary>Uma pendência e os elementos que ela envolve (para selecionar no modelo).</summary>
public sealed record Pendencia(GravidadeDaPendencia Gravidade, string Grupo, string Descricao, IReadOnlyList<long> Elementos);

/// <summary>Resultado da verificação: pendências em ordem de gravidade e o que foi verificado.</summary>
/// <param name="MemoriasConferidas">Se todas as memórias gravadas foram comparadas com o cálculo atual (exige as condições da rodada).</param>
public sealed record RelatorioDeVerificacao(IReadOnlyList<Pendencia> Pendencias, int Pontos, int Circuitos, bool MemoriasConferidas)
{
    private const int ElementosListados = 20;

    public int Contar(GravidadeDaPendencia gravidade) => Pendencias.Count(pendencia => pendencia.Gravidade == gravidade);

    /// <summary>Relatório em Markdown, sem data nem hora: o mesmo modelo dá o mesmo texto.</summary>
    public string Markdown()
    {
        var texto = new System.Text.StringBuilder();
        texto.Append("# Verificação do projeto\n\n");
        texto.Append($"- **Pontos de carga verificados:** {Pontos}\n");
        texto.Append($"- **Circuitos de força verificados:** {Circuitos}\n");
        texto.Append($"- **Memórias conferidas com o modelo:** {(MemoriasConferidas ? "sim" : "não todas (há memórias sem as condições da rodada que as gerou)")}\n");
        texto.Append($"- **Pendências:** {Contar(GravidadeDaPendencia.Erro)} erro(s), {Contar(GravidadeDaPendencia.Aviso)} aviso(s), {Contar(GravidadeDaPendencia.Informacao)} informação(ões)\n");
        if (Pendencias.Count == 0)
        {
            texto.Append("\nSem pendências.\n");
            return texto.ToString();
        }

        foreach (var gravidade in Enum.GetValues<GravidadeDaPendencia>())
        {
            var daGravidade = Pendencias.Where(pendencia => pendencia.Gravidade == gravidade).ToList();
            if (daGravidade.Count == 0) continue;

            texto.Append($"\n## {Titulo(gravidade)}\n");
            foreach (var grupo in daGravidade.GroupBy(pendencia => pendencia.Grupo))
            {
                texto.Append($"\n### {grupo.Key}\n\n");
                foreach (var pendencia in grupo)
                {
                    texto.Append($"- {pendencia.Descricao}");
                    if (pendencia.Elementos.Count > 0)
                    {
                        texto.Append($" (elementos: {string.Join(", ", pendencia.Elementos.Take(ElementosListados))}");
                        texto.Append(pendencia.Elementos.Count > ElementosListados ? $" e mais {pendencia.Elementos.Count - ElementosListados})" : ")");
                    }

                    texto.Append('\n');
                }
            }
        }

        return texto.ToString();
    }

    public static string Titulo(GravidadeDaPendencia gravidade) => gravidade switch
    {
        GravidadeDaPendencia.Erro => "Erros",
        GravidadeDaPendencia.Aviso => "Avisos",
        _ => "Informações"
    };
}

/// <summary>
///     Caso de uso "Verificar projeto": lista o que falta ou mudou, sem gravar nada — pontos sem classificação, fora de
///     circuito ou sem local; circuitos criados fora do Ampere; circuitos com dados faltando, não dimensionados ou com a
///     memória gravada diferente da que o modelo daria hoje; quadros sem quadro de cargas ou com ele desatualizado; e onde
///     o cálculo para.
/// </summary>
/// <remarks>
///     A memória de cada circuito é refeita com as condições da rodada que o dimensionou (guardadas no circuito; sem
///     elas, as do projeto), o perfil e os catálogos — pelo mesmo <see cref="DimensionamentoDoProjeto.Calcular" /> do
///     comando "Dimensionar". Circuito com memória e sem condições guardadas não é conferido, e o relatório diz por quê.
/// </remarks>
public static class VerificacaoDoProjeto
{
    public const string PontosSemClassificacao = "Pontos sem classificação";
    public const string PontosForaDeCircuito = "Pontos fora de circuito";
    public const string PontosSemLocal = "Pontos sem local";
    public const string PontosSemAparelho = "Pontos TUE sem aparelho";
    public const string CircuitosForaDoAmpere = "Circuitos criados fora do Ampere";
    public const string CircuitosComDadosFaltando = "Circuitos com dados faltando";
    public const string CircuitosNaoDimensionados = "Circuitos não dimensionados";
    public const string MemoriasDesatualizadas = "Memórias desatualizadas";
    public const string AvisosDoDimensionamento = "Avisos do dimensionamento";
    public const string CondicoesNaoGuardadas = "Memórias sem as condições da rodada";
    public const string CalculoInterrompido = "Onde o cálculo para";
    public const string ResultadosEditados = "Resultados editados à mão";
    public const string QuadrosNaoMontados = "Quadros sem quadro de cargas";
    public const string QuadrosDesatualizados = "Quadros de cargas desatualizados";
    public const string AlimentadoresNaoDimensionados = "Alimentadores não dimensionados";
    public const string AlimentadoresDesatualizados = "Alimentadores desatualizados";
    public const string AlimentadoresSemCalculo = "Alimentadores que o Ampere não dimensiona assim";

    /// <param name="quadros">Os quadros, para conferir o quadro de cargas de cada um; nulo = não conferir.</param>
    /// <param name="alimentadores">Os alimentadores dos quadros, para conferir cada um (exige <paramref name="quadros" />); nulo = não conferir.</param>
    public static RelatorioDeVerificacao Executar(
        IDocumentoDeVerificacao documento, PerfilNormativo perfil, CatalogosDeProduto catalogos, IDocumentoDeQuadros? quadros = null,
        IDocumentoDeAlimentadores? alimentadores = null)
    {
        var pendencias = new List<Pendencia>();
        var pontos = documento.LerPontos();
        var circuitos = documento.LerCircuitosDeForca();
        var doAmpere = circuitos.Where(circuito => !string.IsNullOrWhiteSpace(circuito.Numero)).ToList();
        var dados = documento.LerCircuitos(doAmpere.Select(circuito => circuito.Id).ToList()).ToDictionary(circuito => circuito.Id);

        var classificados = Pontos(pontos, pendencias);
        PontosSemLocalDecidido(classificados, dados, pendencias);
        Adicionar(pendencias, GravidadeDaPendencia.Aviso, CircuitosForaDoAmpere,
            circuitos.Where(circuito => string.IsNullOrWhiteSpace(circuito.Numero) && classificados.Any(ponto => ponto.Circuito == circuito.Id)).ToList(),
            quantos => $"{quantos} circuito(s) com pontos classificados e sem AMP_NumeroCircuito: o Ampere não os dimensiona nem os põe no quadro de cargas (apague o circuito no Revit e refaça-o com 'Criar circuitos', que não mexe em ponto já circuitado)");

        var conferidas = Circuitos(doAmpere, dados, documento.LerCondicoes(), documento.LerCondicoesDosCircuitos(dados.Keys.ToList()),
            documento.LerResultados(dados.Keys.ToList()), perfil, catalogos, pendencias);
        if (quadros is not null) conferidas &= Quadros(quadros, perfil, pendencias);
        if (quadros is not null && alimentadores is not null) Alimentadores(alimentadores, quadros, perfil, catalogos, pendencias);

        var ordenadas = pendencias
            .OrderBy(pendencia => pendencia.Gravidade)
            .ThenBy(pendencia => Array.IndexOf(OrdemDosGrupos, pendencia.Grupo))
            .ThenBy(pendencia => pendencia.Descricao, StringComparer.Ordinal)
            .ToList();
        return new RelatorioDeVerificacao(ordenadas, pontos.Count, circuitos.Count, conferidas);
    }

    private static readonly string[] OrdemDosGrupos =
    [
        PontosSemClassificacao, PontosForaDeCircuito, PontosSemLocal, PontosSemAparelho, CircuitosForaDoAmpere, CircuitosComDadosFaltando,
        CircuitosNaoDimensionados, MemoriasDesatualizadas, ResultadosEditados, QuadrosNaoMontados, QuadrosDesatualizados, AlimentadoresNaoDimensionados,
        AlimentadoresDesatualizados, AvisosDoDimensionamento, CondicoesNaoGuardadas, CalculoInterrompido, AlimentadoresSemCalculo
    ];

    // Cada quadro de cargas é refeito com os fatores guardados na montagem e comparado com o hash gravado no quadro.
    // Devolve se todos os quadros montados puderam ser conferidos.
    private static bool Quadros(IDocumentoDeQuadros quadros, PerfilNormativo perfil, List<Pendencia> pendencias)
    {
        var naoMontados = new List<(long Id, string Nome)>();
        var desatualizados = new List<(long Id, string Nome)>();
        var semFatores = new List<(long Id, string Nome)>();
        foreach (var quadro in quadros.LerQuadrosComCircuitos())
        {
            if (quadros.LerMemoriaDoQuadro(quadro.Id) is not { } gravada)
            {
                naoMontados.Add((quadro.Id, quadro.Nome));
                continue;
            }

            if (quadros.LerFatoresDoQuadro(quadro.Id) is not { } fatores)
            {
                semFatores.Add((quadro.Id, quadro.Nome));
                continue;
            }

            if (QuadroDeCargasDoProjeto.Montar(quadro, perfil, fatores).Quadro.Memoria?.Hash() != gravada.Trim()) desatualizados.Add((quadro.Id, quadro.Nome));
        }

        AdicionarQuadros(pendencias, GravidadeDaPendencia.Aviso, QuadrosNaoMontados, naoMontados,
            quantos => $"{quantos} quadro(s) com circuitos e sem quadro de cargas montado: rode 'Montar quadro de cargas'");
        AdicionarQuadros(pendencias, GravidadeDaPendencia.Aviso, QuadrosDesatualizados, desatualizados,
            quantos => $"{quantos} quadro(s) com o quadro de cargas gravado diferente do que o modelo dá hoje (circuitos, potências ou alimentação mudaram): rode 'Montar quadro de cargas' de novo");
        AdicionarQuadros(pendencias, GravidadeDaPendencia.Informacao, CondicoesNaoGuardadas, semFatores,
            quantos => $"{quantos} quadro(s) montado(s) sem os fatores guardados (montagem anterior a esta versão): não conferidos — rode 'Montar quadro de cargas' para conferi-los depois");
        return semFatores.Count == 0;
    }

    // Cada alimentador refeito sem gravar, para cada origem da instalação (a escolhida na rodada não fica no modelo): em dia
    // se a memória gravada confere com a de alguma origem. Sem memória gravada: não dimensionado, se algum cálculo sai, ou
    // sem cálculo (o motivo da primeira origem).
    private static void Alimentadores(
        IDocumentoDeAlimentadores alimentadores, IDocumentoDeQuadros quadros, PerfilNormativo perfil, CatalogosDeProduto catalogos, List<Pendencia> pendencias)
    {
        var porOrigem = DimensionamentoDeAlimentadores.Origens.Keys
            .Select(origem => DimensionamentoDeAlimentadores.Calcular(origem, perfil, catalogos, alimentadores, quadros))
            .ToList();
        var comCircuito = porOrigem[0].Where(resultado => resultado.Circuito is not null).ToList();
        if (comCircuito.Count == 0) return;

        var gravados = alimentadores.LerResultados(comCircuito.Select(resultado => resultado.Circuito!.Id).ToList());
        var naoDimensionados = new List<(long Id, string Nome)>();
        var desatualizados = new List<(long Id, string Nome)>();
        var semCalculo = new List<(long Id, string Nome, string Motivo)>();
        foreach (var resultado in comCircuito)
        {
            var id = resultado.Circuito!.Id;
            var calculadas = porOrigem
                .Select(resultados => resultados.Single(outro => outro.QuadroId == resultado.QuadroId).Circuito?.Memoria?.Hash())
                .OfType<string>()
                .ToList();
            var nome = $"alimentador do {resultado.Quadro}";
            if (gravados.GetValueOrDefault(id)?.MemoriaCalculoId?.Trim() is not { Length: > 0 } gravada)
            {
                if (calculadas.Count > 0) naoDimensionados.Add((id, nome));
                else semCalculo.Add((id, nome, resultado.Problemas.FirstOrDefault() ?? "sem cálculo"));
            }
            else if (!calculadas.Contains(gravada, StringComparer.Ordinal))
            {
                desatualizados.Add((id, nome));
            }
        }

        AdicionarQuadros(pendencias, GravidadeDaPendencia.Aviso, AlimentadoresNaoDimensionados, naoDimensionados,
            quantos => $"{quantos} alimentador(es) que o Ampere pode dimensionar e ainda sem resultado: rode 'Dimensionar alimentadores'");
        AdicionarQuadros(pendencias, GravidadeDaPendencia.Aviso, AlimentadoresDesatualizados, desatualizados,
            quantos => $"{quantos} alimentador(es) com a memória gravada diferente da que o modelo dá hoje (quadro, terminais ou o próprio alimentador mudaram): rode 'Dimensionar alimentadores' de novo");
        foreach (var (id, nome, motivo) in semCalculo)
            pendencias.Add(new Pendencia(GravidadeDaPendencia.Informacao, AlimentadoresSemCalculo, $"{nome}: {motivo}", [id]));
    }

    private static void AdicionarQuadros(List<Pendencia> pendencias, GravidadeDaPendencia gravidade, string grupo, List<(long Id, string Nome)> quadros, Func<int, string> descricao)
    {
        if (quadros.Count == 0) return;

        var exemplos = string.Join(", ", quadros.Take(5).Select(quadro => quadro.Nome)) + (quadros.Count > 5 ? ", …" : string.Empty);
        pendencias.Add(new Pendencia(gravidade, grupo, $"{descricao(quadros.Count)} — {exemplos}", quadros.Select(quadro => quadro.Id).ToList()));
    }

    // Devolve os pontos classificados, que seguem para as verificações de circuito e de local. Reserva não é tipo de
    // ponto ("Classificar cargas" e "Criar circuitos" o recusam): conta como sem classificação.
    private static List<PontoVerificado> Pontos(IReadOnlyList<PontoVerificado> pontos, List<Pendencia> pendencias)
    {
        static bool Classificado(PontoVerificado ponto) => CodigosDeTipoDeCarga.TryLer(ponto.TipoDeCarga, out var tipo) && tipo != TipoDeCarga.Reserva;

        var semTipo = pontos.Where(ponto => !Classificado(ponto)).Select(ponto => ponto.Id).ToList();
        if (semTipo.Count > 0)
        {
            pendencias.Add(new Pendencia(GravidadeDaPendencia.Aviso, PontosSemClassificacao,
                $"{semTipo.Count} ponto(s) de carga sem AMP_TipoCarga reconhecido (ou com Reserva, que não vale para ponto): ficam fora dos circuitos e do quadro de cargas (rode 'Classificar cargas')", semTipo));
        }

        var classificados = pontos.Where(Classificado).ToList();
        var semAparelho = classificados
            .Where(ponto => CodigosDeTipoDeCarga.TryLer(ponto.TipoDeCarga, out var tipo) && tipo == TipoDeCarga.TUE && !CodigosDeAparelho.TryLer(ponto.Aparelho, out _))
            .Select(ponto => ponto.Id)
            .ToList();
        if (semAparelho.Count > 0)
        {
            pendencias.Add(new Pendencia(GravidadeDaPendencia.Informacao, PontosSemAparelho,
                $"{semAparelho.Count} ponto(s) TUE sem AMP_Aparelho: o dimensionamento não precisa dele, mas a 'Demanda da entrada' para (rode 'Classificar cargas' e escolha o aparelho, ou 'Outro')", semAparelho));
        }

        var foraDeCircuito = classificados.Where(ponto => ponto.Circuito is null).Select(ponto => ponto.Id).ToList();
        if (foraDeCircuito.Count > 0)
        {
            pendencias.Add(new Pendencia(GravidadeDaPendencia.Erro, PontosForaDeCircuito,
                $"{foraDeCircuito.Count} ponto(s) classificado(s) fora de circuito: a carga não entra em nenhum quadro (rode 'Criar circuitos')", foraDeCircuito));
        }

        return classificados;
    }

    // Sem local, a tabela de IDR não decide; com decisão do projetista sobre o IDR no circuito, o local não faz falta.
    // Só nos circuitos do Ampere: ponto fora de circuito (ou em circuito de fora) já tem a sua pendência.
    private static void PontosSemLocalDecidido(List<PontoVerificado> classificados, Dictionary<long, DadosDoCircuito> dados, List<Pendencia> pendencias)
    {
        var semLocal = classificados
            .Where(ponto => string.IsNullOrWhiteSpace(ponto.Local))
            .Where(ponto => ponto.Circuito is { } circuito && dados.TryGetValue(circuito, out var doCircuito)
                                                         && string.IsNullOrWhiteSpace(doCircuito.Decisoes?.Idr))
            .Select(ponto => ponto.Id)
            .ToList();
        if (semLocal.Count == 0) return;

        pendencias.Add(new Pendencia(GravidadeDaPendencia.Erro, PontosSemLocal,
            $"{semLocal.Count} ponto(s) sem AMP_Local e sem decisão do projetista sobre o IDR no circuito: o dimensionamento para no IDR (rode 'Locais pelos ambientes' ou 'Classificar cargas')", semLocal));
    }

    // Devolve se todas as memórias gravadas puderam ser conferidas (havia as condições da rodada de cada uma).
    private static bool Circuitos(
        List<CircuitoVerificado> doAmpere, Dictionary<long, DadosDoCircuito> dados, CondicoesDoProjeto? doProjeto,
        IReadOnlyDictionary<long, CondicoesDoProjeto> porCircuito, IReadOnlyDictionary<long, ResultadosNoCircuito> gravados, PerfilNormativo perfil,
        CatalogosDeProduto catalogos, List<Pendencia> pendencias)
    {
        var naoDimensionados = new List<CircuitoVerificado>();
        var desatualizados = new List<CircuitoVerificado>();
        var semCondicoes = new List<CircuitoVerificado>();
        var interrupcoes = new Dictionary<string, List<long>>(StringComparer.Ordinal);
        foreach (var circuito in doAmpere)
        {
            if (!dados.TryGetValue(circuito.Id, out var doCircuito)) continue;

            var gravada = string.IsNullOrWhiteSpace(circuito.MemoriaGravada) ? null : circuito.MemoriaGravada.Trim();
            if ((porCircuito.GetValueOrDefault(circuito.Id) ?? doProjeto) is not { } condicoes)
            {
                (gravada is null ? naoDimensionados : semCondicoes).Add(circuito);
                continue;
            }

            var resultado = DimensionamentoDoProjeto.Calcular(doCircuito, condicoes, perfil, catalogos);
            var problemasDeEntrada = resultado.ProblemasDeDados.Count > 0
                ? resultado.ProblemasDeDados
                : resultado.Dimensionamento is { Situacao: SituacaoDoDimensionamento.EntradaInvalida } invalido ? invalido.Problemas : null;
            if (problemasDeEntrada is not null)
            {
                pendencias.Add(new Pendencia(GravidadeDaPendencia.Erro, CircuitosComDadosFaltando,
                    $"{Identificacao(circuito)}: {string.Join("; ", problemasDeEntrada)}", [circuito.Id]));
                continue;
            }

            if (gravada is null) naoDimensionados.Add(circuito);
            else if (resultado.Memoria is not { } memoria || memoria.Hash() != gravada) desatualizados.Add(circuito);
            else if (gravados.TryGetValue(circuito.Id, out var noModelo) && noModelo.Diferencas(ResultadosNoCircuito.De(resultado)) is { Count: > 0 } diferencas)
            {
                // A memória confere, mas o parâmetro não é o que ela justifica: alguém mexeu no resultado sem dimensionar.
                pendencias.Add(new Pendencia(GravidadeDaPendencia.Aviso, ResultadosEditados,
                    $"{Identificacao(circuito)}: {string.Join("; ", diferencas)} (rode 'Dimensionar circuitos' ou registre a decisão nos AMP_*Projetista)",
                    [circuito.Id]));
            }

            foreach (var aviso in resultado.Dimensionamento!.Avisos)
                pendencias.Add(new Pendencia(GravidadeDaPendencia.Aviso, AvisosDoDimensionamento, $"{Identificacao(circuito)}: {aviso}", [circuito.Id]));

            foreach (var problema in resultado.Dimensionamento.Problemas)
            {
                if (!interrupcoes.TryGetValue(problema, out var ids)) interrupcoes[problema] = ids = [];
                ids.Add(circuito.Id);
            }
        }

        Adicionar(pendencias, GravidadeDaPendencia.Aviso, CircuitosNaoDimensionados, naoDimensionados,
            quantos => $"{quantos} circuito(s) sem memória gravada: rode 'Dimensionar circuitos'");
        Adicionar(pendencias, GravidadeDaPendencia.Aviso, MemoriasDesatualizadas, desatualizados,
            quantos => $"{quantos} circuito(s) com a memória gravada diferente da que o modelo dá hoje com as condições da rodada que os dimensionou (dados, decisões, perfil ou catálogos mudaram): rode 'Dimensionar circuitos' de novo");
        Adicionar(pendencias, GravidadeDaPendencia.Informacao, CondicoesNaoGuardadas, semCondicoes,
            quantos => $"{quantos} circuito(s) com memória, mas sem as condições da rodada que os dimensionou (rodada anterior a esta versão): não conferidos — rode 'Dimensionar circuitos' para conferi-los depois");
        foreach (var (problema, ids) in interrupcoes)
            pendencias.Add(new Pendencia(GravidadeDaPendencia.Informacao, CalculoInterrompido, $"{problema} ({ids.Count} circuito(s))", ids));
        return semCondicoes.Count == 0;
    }

    private static void Adicionar(List<Pendencia> pendencias, GravidadeDaPendencia gravidade, string grupo, IReadOnlyList<CircuitoVerificado> circuitos,
        Func<int, string> descricao)
    {
        if (circuitos.Count == 0) return;

        var exemplos = string.Join(", ", circuitos.Take(5).Select(Identificacao)) + (circuitos.Count > 5 ? ", …" : string.Empty);
        pendencias.Add(new Pendencia(gravidade, grupo, $"{descricao(circuitos.Count)} — {exemplos}", circuitos.Select(circuito => circuito.Id).ToList()));
    }

    private static string Identificacao(CircuitoVerificado circuito)
    {
        var numero = string.IsNullOrWhiteSpace(circuito.Numero) ? circuito.Nome : circuito.Numero;
        return string.IsNullOrWhiteSpace(circuito.Quadro) ? numero : $"{circuito.Quadro} {numero}";
    }
}
