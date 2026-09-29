using System.Globalization;
using Ampere.Core.Cargas;
using Ampere.Core.Normas;

namespace Ampere.Core.Dimensionamento;

/// <summary>Valores AMP_* de um ponto do circuito, como o adapter os lê (nulo = parâmetro vazio).</summary>
public sealed record DadosDoPonto(long Id, decimal? PotenciaVA, decimal? TensaoV, string? Fases);

/// <summary>Valores AMP_* de um circuito e dos seus pontos, como o adapter os lê (nulo = parâmetro vazio).</summary>
/// <param name="Id">Identificador do circuito no documento.</param>
/// <param name="Numero">AMP_NumeroCircuito.</param>
/// <param name="TipoDeCarga">AMP_TipoCarga (texto).</param>
/// <param name="ComprimentoM">AMP_ComprimentoRotaM, em m.</param>
/// <param name="MetodoDeInstalacao">AMP_MetodoInstalacao.</param>
/// <param name="Isolacao">AMP_MaterialIsolacao.</param>
/// <param name="Pontos">Pontos de carga do circuito.</param>
public sealed record DadosDoCircuito(
    long Id,
    string? Numero,
    string? TipoDeCarga,
    decimal? ComprimentoM,
    string? MetodoDeInstalacao,
    string? Isolacao,
    IReadOnlyList<DadosDoPonto> Pontos);

/// <summary>
///     Condições do projeto que não são parâmetros do circuito, informadas pelo projetista.
/// </summary>
/// <param name="TemperaturaAmbienteC">Temperatura ambiente, em °C.</param>
/// <param name="CircuitosAgrupados">Circuitos agrupados (para o FCA).</param>
/// <param name="Material">Material dos condutores, ex.: Cobre.</param>
/// <param name="MetodoDeInstalacaoPadrao">Usado quando o circuito não tem AMP_MetodoInstalacao.</param>
/// <param name="IsolacaoPadrao">Usada quando o circuito não tem AMP_MaterialIsolacao.</param>
public sealed record CondicoesDoProjeto(
    decimal TemperaturaAmbienteC,
    int CircuitosAgrupados,
    string Material,
    string? MetodoDeInstalacaoPadrao = null,
    string? IsolacaoPadrao = null);

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

        if (dados.Pontos.Count == 0) problemas.Add("circuito sem pontos");
        foreach (var ponto in dados.Pontos.Where(ponto => ponto.PotenciaVA is null))
            problemas.Add($"ponto {ponto.Id} sem AMP_PotenciaInstaladaVA");

        var tensao = Comum(dados.Pontos.Select(ponto => ponto.TensaoV).OfType<decimal>().Select(PerfilNormativo.Numero), "AMP_TensaoCircuitoV", "tensões", problemas);
        var fases = Comum(dados.Pontos.Select(ponto => ponto.Fases).OfType<string>(), "AMP_Fases", "fases", problemas);
        if (problemas.Count > 0) return new EntradaMontada(null, problemas);

        var entrada = new EntradaDeDimensionamento(
            dados.Numero!,
            tipo,
            dados.Pontos.Sum(ponto => ponto.PotenciaVA!.Value),
            fases!,
            decimal.Parse(tensao!, CultureInfo.InvariantCulture),
            dados.ComprimentoM!.Value,
            metodo!,
            isolacao!,
            condicoes.Material,
            condicoes.TemperaturaAmbienteC,
            condicoes.CircuitosAgrupados);
        return new EntradaMontada(entrada, []);
    }

    private static string? Preencher(string? valor, string? padrao, string parametro, List<string> problemas)
    {
        var escolhido = string.IsNullOrWhiteSpace(valor) ? padrao : valor;
        if (string.IsNullOrWhiteSpace(escolhido)) problemas.Add($"sem {parametro} nem padrão do projeto");
        return escolhido;
    }

    private static string? Comum(IEnumerable<string> valores, string parametro, string nome, List<string> problemas)
    {
        var distintos = valores.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (distintos.Count == 0) problemas.Add($"nenhum ponto com {parametro}");
        else if (distintos.Count > 1) problemas.Add($"pontos com {nome} diferentes ({string.Join(", ", distintos)})");
        return distintos.Count == 1 ? distintos[0] : null;
    }
}

/// <summary>Entrada pronta (nula se houver problema) e os problemas dos dados do circuito.</summary>
public sealed record EntradaMontada(EntradaDeDimensionamento? Entrada, IReadOnlyList<string> Problemas);
