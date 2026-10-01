using Ampere.Core.Locais;

namespace Ampere.ViewModels;

/// <summary>
///     Diálogo "Locais pelos ambientes": um local da tabela de proteção diferencial do perfil para cada nome de ambiente.
///     Vem preenchido com o local que os pontos do ambiente já têm em comum ou, senão, com a escolha anterior da sessão
///     para o mesmo nome; o Ampere não deduz o local pelo nome.
/// </summary>
public sealed class LocaisPorAmbienteViewModel : ObservableObject
{
    public LocaisPorAmbienteViewModel(
        IReadOnlyList<AmbienteComPontos> ambientes, IReadOnlyList<string> locaisDoPerfil, int semAmbiente, int naoEditaveis,
        IReadOnlyDictionary<string, string> escolhasAnteriores)
    {
        IReadOnlyList<string> opcoes = [string.Empty, .. locaisDoPerfil];
        // Só pré-preenche com local do perfil: um "Banheiro" digitado à mão ficaria invisível no combo e recusaria tudo.
        string? DoPerfil(string? local) => local is not null && locaisDoPerfil.Contains(local, StringComparer.Ordinal) ? local : null;
        Linhas = ambientes
            .Select(ambiente => new LinhaDeAmbiente(ambiente.Nome, ambiente.Pontos, opcoes,
                DoPerfil(ambiente.LocalComum) ?? DoPerfil(escolhasAnteriores.GetValueOrDefault(ambiente.Nome)) ?? string.Empty))
            .ToList();
        Titulo = $"{ambientes.Sum(ambiente => ambiente.Pontos)} ponto(s) em {ambientes.Count} ambiente(s)";
        var avisos = new List<string>();
        if (semAmbiente > 0) avisos.Add($"{semAmbiente} ponto(s) fora de qualquer ambiente: informe o local pelo 'Classificar cargas'.");
        if (naoEditaveis > 0) avisos.Add($"{naoEditaveis} ponto(s) em grupo ou vínculo não serão alterados.");
        Avisos = string.Join(Environment.NewLine, avisos);
    }

    public string Titulo { get; }

    public string Avisos { get; }

    public IReadOnlyList<LinhaDeAmbiente> Linhas { get; }

    /// <summary>Local escolhido por nome de ambiente (só os preenchidos), depois de <see cref="Confirmar" />.</summary>
    public IReadOnlyDictionary<string, string>? Escolhas { get; private set; }

    public bool Confirmar()
    {
        Escolhas = Linhas
            .Where(linha => !string.IsNullOrWhiteSpace(linha.Local))
            .ToDictionary(linha => linha.Ambiente, linha => linha.Local, StringComparer.OrdinalIgnoreCase);
        return true;
    }
}

/// <summary>Um ambiente do diálogo: nome, quantos pontos tem e o local escolhido (vazio = não alterar).</summary>
public sealed class LinhaDeAmbiente(string ambiente, int pontos, IReadOnlyList<string> opcoes, string local) : ObservableObject
{
    private string _local = local;

    public string Ambiente { get; } = ambiente;

    public string Pontos { get; } = $"{pontos} ponto(s)";

    public IReadOnlyList<string> Opcoes { get; } = opcoes;

    public string Local
    {
        get => _local;
        set => SetProperty(ref _local, value);
    }
}
