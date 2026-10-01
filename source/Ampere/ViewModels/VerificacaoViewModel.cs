using Ampere.Core.Verificacao;

namespace Ampere.ViewModels;

/// <summary>Uma pendência na lista do diálogo.</summary>
public sealed record LinhaDePendencia(string Gravidade, string Grupo, string Descricao, IReadOnlyList<long> Elementos)
{
    public string Quantidade => Elementos.Count == 0 ? string.Empty : $"{Elementos.Count} elemento(s)";
}

/// <summary>
///     Diálogo "Verificar projeto": as pendências em ordem de gravidade; a escolhida pode ser selecionada no modelo.
/// </summary>
public sealed class VerificacaoViewModel : ObservableObject
{
    private LinhaDePendencia? _escolhida;

    /// <param name="arquivo">Onde o relatório em Markdown foi salvo (nulo se não foi).</param>
    public VerificacaoViewModel(RelatorioDeVerificacao relatorio, string? arquivo)
    {
        Linhas = relatorio.Pendencias
            .Select(pendencia => new LinhaDePendencia(Gravidade(pendencia.Gravidade), pendencia.Grupo, pendencia.Descricao, pendencia.Elementos))
            .ToList();
        Titulo = relatorio.Pendencias.Count == 0
            ? $"{relatorio.Pontos} ponto(s) e {relatorio.Circuitos} circuito(s) verificados: sem pendências."
            : $"{relatorio.Pontos} ponto(s) e {relatorio.Circuitos} circuito(s) verificados: {relatorio.Contar(GravidadeDaPendencia.Erro)} erro(s), " +
              $"{relatorio.Contar(GravidadeDaPendencia.Aviso)} aviso(s), {relatorio.Contar(GravidadeDaPendencia.Informacao)} informação(ões).";
        Rodape = arquivo is null ? "O relatório não pôde ser salvo." : $"Relatório: {arquivo}";
    }

    public string Titulo { get; }

    public string Rodape { get; }

    public IReadOnlyList<LinhaDePendencia> Linhas { get; }

    public LinhaDePendencia? Escolhida
    {
        get => _escolhida;
        set
        {
            if (SetProperty(ref _escolhida, value)) OnPropertyChanged(nameof(PodeSelecionar));
        }
    }

    public bool PodeSelecionar => Escolhida is { Elementos.Count: > 0 };

    /// <summary>Elementos a selecionar no modelo ao fechar (nulo = não mexer na seleção).</summary>
    public IReadOnlyList<long>? ParaSelecionar { get; private set; }

    public bool Selecionar()
    {
        if (!PodeSelecionar) return false;
        ParaSelecionar = Escolhida!.Elementos;
        return true;
    }

    private static string Gravidade(GravidadeDaPendencia gravidade) => gravidade switch
    {
        GravidadeDaPendencia.Erro => "Erro",
        GravidadeDaPendencia.Aviso => "Aviso",
        _ => "Info"
    };
}
