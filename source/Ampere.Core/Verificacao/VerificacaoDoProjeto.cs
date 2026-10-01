using Ampere.Core.Cargas;
using Ampere.Core.Catalogos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;

namespace Ampere.Core.Verificacao;

/// <summary>Ponto de carga do documento (família com conector elétrico de força), como o adapter o lê.</summary>
/// <param name="TipoDeCarga">AMP_TipoCarga (texto; nulo = vazio).</param>
/// <param name="Local">AMP_Local (nulo = vazio).</param>
/// <param name="Circuito">Circuito de força de que o ponto é membro (nulo = fora de circuito).</param>
public sealed record PontoVerificado(long Id, string? TipoDeCarga, string? Local, long? Circuito);

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

    /// <summary>Condições guardadas pela última rodada do dimensionamento (nulas se nunca guardadas).</summary>
    CondicoesDoProjeto? LerCondicoes();
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
/// <param name="MemoriasConferidas">Se as memórias gravadas foram comparadas com o cálculo atual (exige condições guardadas).</param>
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
        texto.Append($"- **Memórias conferidas com o modelo:** {(MemoriasConferidas ? "sim" : "não (o modelo não tem as condições da última rodada do 'Dimensionar')")}\n");
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
///     memória gravada diferente da que o modelo daria hoje; e onde o cálculo para.
/// </summary>
/// <remarks>
///     A memória é refeita com as condições guardadas no modelo pela última rodada do dimensionamento, o perfil e os
///     catálogos — o mesmo caminho do comando "Dimensionar". Sem condições guardadas, a conferência dos circuitos fica
///     de fora, e o relatório diz por quê.
/// </remarks>
public static class VerificacaoDoProjeto
{
    public const string PontosSemClassificacao = "Pontos sem classificação";
    public const string PontosForaDeCircuito = "Pontos fora de circuito";
    public const string PontosSemLocal = "Pontos sem local";
    public const string CircuitosForaDoAmpere = "Circuitos criados fora do Ampere";
    public const string CircuitosComDadosFaltando = "Circuitos com dados faltando";
    public const string CircuitosNaoDimensionados = "Circuitos não dimensionados";
    public const string MemoriasDesatualizadas = "Memórias desatualizadas";
    public const string AvisosDoDimensionamento = "Avisos do dimensionamento";
    public const string CondicoesNaoGuardadas = "Condições do projeto não guardadas";
    public const string CalculoInterrompido = "Onde o cálculo para";

    public static RelatorioDeVerificacao Executar(IDocumentoDeVerificacao documento, PerfilNormativo perfil, CatalogosDeProduto catalogos)
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
            quantos => $"{quantos} circuito(s) com pontos classificados e sem AMP_NumeroCircuito: o Ampere não os dimensiona nem os põe no quadro de cargas (refaça-os com 'Criar circuitos')");

        var condicoes = documento.LerCondicoes();
        if (condicoes is null)
        {
            if (doAmpere.Count > 0)
            {
                pendencias.Add(new Pendencia(GravidadeDaPendencia.Informacao, CondicoesNaoGuardadas,
                    "o modelo não tem as condições da última rodada do 'Dimensionar': rode-o para que a verificação confira os circuitos e as memórias", []));
            }
        }
        else
        {
            Circuitos(doAmpere, dados, condicoes, perfil, catalogos, pendencias);
        }

        var ordenadas = pendencias
            .OrderBy(pendencia => pendencia.Gravidade)
            .ThenBy(pendencia => Array.IndexOf(OrdemDosGrupos, pendencia.Grupo))
            .ThenBy(pendencia => pendencia.Descricao, StringComparer.Ordinal)
            .ToList();
        return new RelatorioDeVerificacao(ordenadas, pontos.Count, circuitos.Count, condicoes is not null);
    }

    private static readonly string[] OrdemDosGrupos =
    [
        PontosSemClassificacao, PontosForaDeCircuito, PontosSemLocal, CircuitosForaDoAmpere, CircuitosComDadosFaltando,
        CircuitosNaoDimensionados, MemoriasDesatualizadas, AvisosDoDimensionamento, CondicoesNaoGuardadas, CalculoInterrompido
    ];

    // Devolve os pontos classificados, que seguem para as verificações de circuito e de local.
    private static List<PontoVerificado> Pontos(IReadOnlyList<PontoVerificado> pontos, List<Pendencia> pendencias)
    {
        var semTipo = pontos.Where(ponto => !CodigosDeTipoDeCarga.TryLer(ponto.TipoDeCarga, out _)).Select(ponto => ponto.Id).ToList();
        if (semTipo.Count > 0)
        {
            pendencias.Add(new Pendencia(GravidadeDaPendencia.Aviso, PontosSemClassificacao,
                $"{semTipo.Count} ponto(s) de carga sem AMP_TipoCarga reconhecido: ficam fora dos circuitos e do quadro de cargas (rode 'Classificar cargas')", semTipo));
        }

        var classificados = pontos.Where(ponto => CodigosDeTipoDeCarga.TryLer(ponto.TipoDeCarga, out _)).ToList();
        var foraDeCircuito = classificados
            .Where(ponto => ponto.Circuito is null && CodigosDeTipoDeCarga.TryLer(ponto.TipoDeCarga, out var tipo) && tipo != TipoDeCarga.Reserva)
            .Select(ponto => ponto.Id)
            .ToList();
        if (foraDeCircuito.Count > 0)
        {
            pendencias.Add(new Pendencia(GravidadeDaPendencia.Erro, PontosForaDeCircuito,
                $"{foraDeCircuito.Count} ponto(s) classificado(s) fora de circuito: a carga não entra em nenhum quadro (rode 'Criar circuitos')", foraDeCircuito));
        }

        return classificados;
    }

    // Sem local, a tabela de IDR não decide; com decisão do projetista sobre o IDR no circuito, o local não faz falta.
    private static void PontosSemLocalDecidido(List<PontoVerificado> classificados, Dictionary<long, DadosDoCircuito> dados, List<Pendencia> pendencias)
    {
        var semLocal = classificados
            .Where(ponto => string.IsNullOrWhiteSpace(ponto.Local))
            .Where(ponto => ponto.Circuito is not { } circuito || !dados.TryGetValue(circuito, out var doCircuito)
                                                                || string.IsNullOrWhiteSpace(doCircuito.Decisoes?.Idr))
            .Select(ponto => ponto.Id)
            .ToList();
        if (semLocal.Count == 0) return;

        pendencias.Add(new Pendencia(GravidadeDaPendencia.Erro, PontosSemLocal,
            $"{semLocal.Count} ponto(s) sem AMP_Local e sem decisão do projetista sobre o IDR no circuito: o dimensionamento para no IDR (rode 'Locais pelos ambientes' ou 'Classificar cargas')", semLocal));
    }

    private static void Circuitos(
        List<CircuitoVerificado> doAmpere, Dictionary<long, DadosDoCircuito> dados, CondicoesDoProjeto condicoes, PerfilNormativo perfil,
        CatalogosDeProduto catalogos, List<Pendencia> pendencias)
    {
        var naoDimensionados = new List<CircuitoVerificado>();
        var desatualizados = new List<CircuitoVerificado>();
        var interrupcoes = new Dictionary<string, List<long>>(StringComparer.Ordinal);
        foreach (var circuito in doAmpere)
        {
            if (!dados.TryGetValue(circuito.Id, out var doCircuito)) continue;

            var montada = EntradaDoCircuito.Montar(doCircuito, condicoes);
            if (montada.Entrada is null)
            {
                pendencias.Add(new Pendencia(GravidadeDaPendencia.Erro, CircuitosComDadosFaltando,
                    $"{Identificacao(circuito)}: {string.Join("; ", montada.Problemas)}", [circuito.Id]));
                continue;
            }

            var resultado = DimensionamentoDeCircuito.Dimensionar(montada.Entrada, perfil, catalogos);
            if (string.IsNullOrWhiteSpace(circuito.MemoriaGravada)) naoDimensionados.Add(circuito);
            else if (resultado.Memoria is not { } memoria || memoria.Hash() != circuito.MemoriaGravada.Trim()) desatualizados.Add(circuito);

            foreach (var aviso in resultado.Avisos)
                pendencias.Add(new Pendencia(GravidadeDaPendencia.Aviso, AvisosDoDimensionamento, $"{Identificacao(circuito)}: {aviso}", [circuito.Id]));

            foreach (var problema in resultado.Problemas)
            {
                if (!interrupcoes.TryGetValue(problema, out var ids)) interrupcoes[problema] = ids = [];
                ids.Add(circuito.Id);
            }
        }

        Adicionar(pendencias, GravidadeDaPendencia.Aviso, CircuitosNaoDimensionados, naoDimensionados,
            quantos => $"{quantos} circuito(s) sem memória gravada: rode 'Dimensionar circuitos'");
        Adicionar(pendencias, GravidadeDaPendencia.Aviso, MemoriasDesatualizadas, desatualizados,
            quantos => $"{quantos} circuito(s) com a memória gravada diferente da que o modelo dá hoje (dados, decisões, condições, perfil ou catálogos mudaram): rode 'Dimensionar circuitos' de novo");
        foreach (var (problema, ids) in interrupcoes)
            pendencias.Add(new Pendencia(GravidadeDaPendencia.Informacao, CalculoInterrompido, $"{problema} ({ids.Count} circuito(s))", ids));
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
