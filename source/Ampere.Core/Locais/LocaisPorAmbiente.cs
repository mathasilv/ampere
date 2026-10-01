namespace Ampere.Core.Locais;

/// <summary>Ponto de carga classificado, com o nome do ambiente do Revit em que está e o local que já tem.</summary>
/// <param name="Ambiente">Nome do ambiente (Room ou Space, inclusive de vínculo); nulo = o ponto não está em ambiente.</param>
/// <param name="LocalAtual">AMP_Local atual (nulo = vazio).</param>
/// <param name="Editavel">O ponto aceita edição (fora de grupo e de vínculo).</param>
public sealed record PontoNoAmbiente(long Id, string? Ambiente, string? LocalAtual, bool Editavel = true);

/// <summary>Um ambiente, os seus pontos e o local que todos já têm em comum (nulo se vazio ou diferente entre eles).</summary>
public sealed record AmbienteComPontos(string Nome, int Pontos, string? LocalComum);

/// <summary>Local a gravar num ponto.</summary>
public sealed record LocalParaGravar(long Ponto, string Local);

/// <summary>
///     Porta para os pontos e ambientes de um documento (implementada pelo adapter Revit).
/// </summary>
public interface IDocumentoDeAmbientes : IDocumentoTransacional
{
    /// <summary>Pontos classificados (com AMP_TipoCarga): os de <paramref name="ids" /> ou, vazio, todos os do documento.</summary>
    IReadOnlyList<PontoNoAmbiente> LerPontos(IReadOnlyCollection<long> ids);

    /// <summary>Grava AMP_Local nos pontos.</summary>
    void GravarLocais(IReadOnlyList<LocalParaGravar> locais);
}

/// <summary>O que a atribuição fez.</summary>
/// <param name="Gravados">Pontos que receberam um local novo.</param>
/// <param name="JaEstavam">Pontos que já tinham o local escolhido.</param>
/// <param name="SemEscolha">Pontos de ambientes sem local escolhido (não alterados).</param>
/// <param name="SemAmbiente">Pontos fora de qualquer ambiente (use 'Classificar cargas').</param>
/// <param name="NaoEditaveis">Pontos em grupo ou vínculo (não alterados).</param>
/// <param name="Problemas">Se houver, nada foi gravado.</param>
public sealed record ResultadoDosLocais(int Gravados, int JaEstavam, int SemEscolha, int SemAmbiente, int NaoEditaveis, IReadOnlyList<string> Problemas);

/// <summary>
///     Caso de uso "Locais pelos ambientes": o projetista escolhe, uma vez por nome de ambiente, o local da tabela de
///     proteção diferencial do perfil, e todos os pontos daquele ambiente recebem AMP_Local — numa única transação.
/// </summary>
/// <remarks>
///     O Ampere não deduz o local pelo nome do ambiente ("Banho" → banheiro): a correspondência é do projetista. Ambientes
///     com o mesmo nome (o banheiro de cada pavimento) aparecem uma vez só.
/// </remarks>
public static class LocaisPorAmbiente
{
    /// <summary>Nome da transação, que aparece no menu Desfazer do Revit.</summary>
    public const string NomeDaTransacao = "Ampere: locais pelos ambientes";

    /// <summary>Ambientes dos pontos, por nome, com o local que todos os pontos editáveis já têm em comum.</summary>
    public static IReadOnlyList<AmbienteComPontos> Agrupar(IReadOnlyList<PontoNoAmbiente> pontos) =>
        pontos
            .Where(ponto => !string.IsNullOrWhiteSpace(ponto.Ambiente))
            .GroupBy(ponto => ponto.Ambiente!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(grupo =>
            {
                var locais = grupo.Select(ponto => string.IsNullOrWhiteSpace(ponto.LocalAtual) ? null : ponto.LocalAtual.Trim()).Distinct().ToList();
                return new AmbienteComPontos(grupo.First().Ambiente!.Trim(), grupo.Count(), locais.Count == 1 ? locais[0] : null);
            })
            .OrderBy(ambiente => ambiente.Nome, StringComparer.CurrentCulture)
            .ToList();

    /// <param name="localPorAmbiente">Local escolhido para cada nome de ambiente; ambiente ausente não é alterado.</param>
    /// <param name="locaisDoPerfil">Locais da tabela de proteção diferencial: o escolhido precisa ser um deles.</param>
    public static ResultadoDosLocais Aplicar(
        IReadOnlyList<PontoNoAmbiente> pontos,
        IReadOnlyDictionary<string, string> localPorAmbiente,
        IReadOnlyCollection<string> locaisDoPerfil,
        IDocumentoDeAmbientes documento)
    {
        var escolhas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var problemas = new List<string>();
        foreach (var (ambiente, local) in localPorAmbiente)
        {
            if (string.IsNullOrWhiteSpace(local)) continue;
            if (!locaisDoPerfil.Contains(local.Trim(), StringComparer.Ordinal)) problemas.Add($"{ambiente}: local '{local}' fora da tabela de proteção diferencial do perfil");
            else escolhas[ambiente.Trim()] = local.Trim();
        }

        var semAmbiente = pontos.Count(ponto => string.IsNullOrWhiteSpace(ponto.Ambiente));
        if (problemas.Count > 0) return new ResultadoDosLocais(0, 0, 0, semAmbiente, 0, problemas);

        var gravar = new List<LocalParaGravar>();
        int jaEstavam = 0, semEscolha = 0, naoEditaveis = 0;
        foreach (var ponto in pontos.Where(ponto => !string.IsNullOrWhiteSpace(ponto.Ambiente)))
        {
            if (!escolhas.TryGetValue(ponto.Ambiente!.Trim(), out var local)) semEscolha++;
            else if (string.Equals(ponto.LocalAtual?.Trim(), local, StringComparison.Ordinal)) jaEstavam++;
            else if (!ponto.Editavel) naoEditaveis++;
            else gravar.Add(new LocalParaGravar(ponto.Id, local));
        }

        if (gravar.Count > 0) documento.EmUmaTransacao(NomeDaTransacao, () => documento.GravarLocais(gravar));
        return new ResultadoDosLocais(gravar.Count, jaEstavam, semEscolha, semAmbiente, naoEditaveis, []);
    }
}
