using Ampere.Core.Cargas;

namespace Ampere.Core.Dimensionamento;

/// <summary>Valores AMP_* de um ponto do circuito, como o adapter os lê (nulo = parâmetro vazio).</summary>
/// <param name="Local">Local do ponto no vocabulário da tabela de IDR do perfil (nulo = sem local).</param>
public sealed record DadosDoPonto(long Id, decimal? PotenciaVA, decimal? TensaoV, string? Fases, string? Local = null);

/// <summary>Valores AMP_* de um circuito e dos seus pontos, como o adapter os lê (nulo = parâmetro vazio).</summary>
/// <param name="Id">Identificador do circuito no documento.</param>
/// <param name="Numero">AMP_NumeroCircuito.</param>
/// <param name="TipoDeCarga">AMP_TipoCarga (texto).</param>
/// <param name="ComprimentoM">AMP_ComprimentoRotaM, em m.</param>
/// <param name="MetodoDeInstalacao">AMP_MetodoInstalacao.</param>
/// <param name="Isolacao">AMP_MaterialIsolacao.</param>
/// <param name="Pontos">Pontos de carga do circuito.</param>
/// <param name="TipoDeCondutor">AMP_TipoCondutor.</param>
/// <param name="IdrDoProjetista">Decisão do projetista sobre o IDR do circuito, se houver.</param>
public sealed record DadosDoCircuito(
    long Id,
    string? Numero,
    string? TipoDeCarga,
    decimal? ComprimentoM,
    string? MetodoDeInstalacao,
    string? Isolacao,
    IReadOnlyList<DadosDoPonto> Pontos,
    string? TipoDeCondutor = null,
    DecisaoDeIdr? IdrDoProjetista = null);

/// <summary>
///     Condições do projeto que não são parâmetros do circuito, informadas pelo projetista.
/// </summary>
/// <param name="TemperaturaAmbienteC">Temperatura ambiente, em °C.</param>
/// <param name="CircuitosAgrupados">Circuitos agrupados (para o FCA).</param>
/// <param name="Material">Material dos condutores, ex.: Cobre.</param>
/// <param name="MetodoDeInstalacaoPadrao">Usado quando o circuito não tem AMP_MetodoInstalacao.</param>
/// <param name="IsolacaoPadrao">Usada quando o circuito não tem AMP_MaterialIsolacao.</param>
/// <param name="TipoDeCondutorPadrao">Usado quando o circuito não tem AMP_TipoCondutor.</param>
/// <param name="TipoDeEletroduto">Tipo de eletroduto do catálogo usado no projeto.</param>
public sealed record CondicoesDoProjeto(
    decimal TemperaturaAmbienteC,
    int CircuitosAgrupados,
    string Material,
    string? MetodoDeInstalacaoPadrao = null,
    string? IsolacaoPadrao = null,
    string? TipoDeCondutorPadrao = null,
    string? TipoDeEletroduto = null);

/// <summary>
///     Monta a entrada do dimensionamento a partir dos dados do circuito: potência = soma dos pontos; tensão e fases =
///     o valor comum dos pontos que o informam (valores diferentes são problema, nunca um deles escolhido).
/// </summary>
public static class EntradaDoCircuito
{
    public static EntradaMontada Montar(DadosDoCircuito dados, CondicoesDoProjeto condicoes)
    {
        var problemas = new List<string>();
        if (string.IsNullOrWhiteSpace(dados.Numero)) problemas.Add("sem AMP_NumeroCircuito (rode 'Criar circuitos')");
        if (!CodigosDeTipoDeCarga.TryLer(dados.TipoDeCarga, out var tipo)) problemas.Add($"AMP_TipoCarga vazio ou desconhecido ('{dados.TipoDeCarga}')");
        if (dados.ComprimentoM is null) problemas.Add("sem AMP_ComprimentoRotaM");
        var metodo = Preencher(dados.MetodoDeInstalacao, condicoes.MetodoDeInstalacaoPadrao, "AMP_MetodoInstalacao", problemas);
        var isolacao = Preencher(dados.Isolacao, condicoes.IsolacaoPadrao, "AMP_MaterialIsolacao", problemas);
        var tipoDeCondutor = Preencher(dados.TipoDeCondutor, condicoes.TipoDeCondutorPadrao, "AMP_TipoCondutor", problemas);
        if (string.IsNullOrWhiteSpace(condicoes.TipoDeEletroduto)) problemas.Add("tipo de eletroduto do projeto não informado");

        if (dados.Pontos.Count == 0) problemas.Add("circuito sem pontos");
        foreach (var ponto in dados.Pontos.Where(ponto => ponto.PotenciaVA is null))
            problemas.Add($"ponto {ponto.Id} sem AMP_PotenciaInstaladaVA");

        var tensoes = dados.Pontos.Select(ponto => ponto.TensaoV).OfType<decimal>().Distinct().Order().ToList();
        Unica(tensoes.Select(NumeroEmTexto.Formatar).ToList(), "AMP_TensaoCircuitoV", "tensões", problemas);
        var fases = dados.Pontos.Select(ponto => ponto.Fases).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        Unica(fases, "AMP_Fases", "fases", problemas);
        if (problemas.Count > 0) return new EntradaMontada(null, problemas);

        var entrada = new EntradaDeDimensionamento(
            dados.Numero!,
            tipo,
            dados.Pontos.Sum(ponto => ponto.PotenciaVA!.Value),
            fases[0],
            tensoes[0],
            dados.ComprimentoM!.Value,
            metodo!,
            isolacao!,
            condicoes.Material,
            condicoes.TemperaturaAmbienteC,
            condicoes.CircuitosAgrupados,
            tipoDeCondutor!,
            condicoes.TipoDeEletroduto!,
            dados.Pontos.Select(ponto => ponto.Local).ToList(),
            dados.IdrDoProjetista);
        return new EntradaMontada(entrada, []);
    }

    private static string? Preencher(string? valor, string? padrao, string parametro, List<string> problemas)
    {
        var escolhido = string.IsNullOrWhiteSpace(valor) ? padrao : valor;
        if (string.IsNullOrWhiteSpace(escolhido)) problemas.Add($"sem {parametro} nem padrão do projeto");
        return escolhido;
    }

    // Lista com "; " porque a vírgula é o separador decimal dos números em texto.
    private static void Unica(IReadOnlyList<string> distintos, string parametro, string nome, List<string> problemas)
    {
        if (distintos.Count == 0) problemas.Add($"nenhum ponto com {parametro}");
        else if (distintos.Count > 1) problemas.Add($"pontos com {nome} diferentes ({string.Join("; ", distintos)})");
    }
}

/// <summary>Entrada pronta (nula se houver problema) e os problemas dos dados do circuito.</summary>
public sealed record EntradaMontada(EntradaDeDimensionamento? Entrada, IReadOnlyList<string> Problemas);
