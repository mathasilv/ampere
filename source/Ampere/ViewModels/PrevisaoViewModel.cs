using Ampere.Core.Previsao;

namespace Ampere.ViewModels;

/// <summary>
///     Diálogo "Previsão de cargas": uma categoria de cômodo (NBR 5410, 9.5.2) para cada nome de ambiente. Vem preenchido
///     com as categorias guardadas no projeto; o Ampere não deduz a categoria pelo nome.
/// </summary>
public sealed class PrevisaoViewModel : ObservableObject
{
    public PrevisaoViewModel(IReadOnlyList<ComodoDoProjeto> comodos, IReadOnlyList<string> categorias, IReadOnlyDictionary<string, string> guardadas)
    {
        IReadOnlyList<string> opcoes = [string.Empty, .. categorias];
        // Só pré-preenche com categoria da lista: uma digitada à mão ficaria invisível no combo e recusaria tudo.
        string DaLista(string? categoria) => categoria is not null && categorias.Contains(categoria, StringComparer.Ordinal) ? categoria : string.Empty;
        Linhas = comodos
            .GroupBy(comodo => comodo.Nome.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(grupo => new LinhaDeComodo(grupo.First().Nome.Trim(), grupo.Count(), opcoes,
                DaLista(guardadas.GetValueOrDefault(grupo.Key))))
            .OrderBy(linha => linha.Nome, StringComparer.CurrentCulture)
            .ToList();
        Titulo = $"{comodos.Count} cômodo(s) com {Linhas.Count} nome(s)";
    }

    public string Titulo { get; }

    public IReadOnlyList<LinhaDeComodo> Linhas { get; }

    /// <summary>Categoria por nome de ambiente (vazia = sem categoria), depois de <see cref="Confirmar" />.</summary>
    public IReadOnlyDictionary<string, string?>? Escolhas { get; private set; }

    public bool Confirmar()
    {
        Escolhas = Linhas.ToDictionary(linha => linha.Nome, linha => string.IsNullOrWhiteSpace(linha.Categoria) ? null : linha.Categoria,
            StringComparer.OrdinalIgnoreCase);
        return true;
    }
}

/// <summary>Um nome de ambiente do diálogo: quantos cômodos têm esse nome e a categoria escolhida.</summary>
public sealed class LinhaDeComodo(string nome, int comodos, IReadOnlyList<string> opcoes, string categoria) : ObservableObject
{
    private string _categoria = categoria;

    public string Nome { get; } = nome;

    public string Comodos { get; } = $"{comodos} cômodo(s)";

    public IReadOnlyList<string> Opcoes { get; } = opcoes;

    public string Categoria
    {
        get => _categoria;
        set => SetProperty(ref _categoria, value);
    }
}
