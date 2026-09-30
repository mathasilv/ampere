using System.Globalization;
using Ampere.Core.Cargas;
using Ampere.Core.Entradas;

namespace Ampere.ViewModels;

/// <summary>
///     Diálogo "Montar quadro de cargas": fator de demanda por tipo de carga. Campo vazio = usar a tabela da norma
///     quando ela existir no perfil; valor informado vence o perfil (a memória registra a fonte).
/// </summary>
public sealed class QuadroDeCargasViewModel : ObservableObject
{
    private string _problemas = string.Empty;

    public QuadroDeCargasViewModel()
    {
        Titulo = "Fatores de demanda por tipo de carga";
        Dica = $"Em branco = usar a tabela da norma (hoje TODO_NORMA). Decimais com '{CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator}', sem separador de milhar.";
        Linhas = NomesPorTipo
            .Select(par => new LinhaDeFator(par.Tipo, par.Nome))
            .ToList();
    }

    private static readonly (TipoDeCarga Tipo, string Nome)[] NomesPorTipo =
    [
        (TipoDeCarga.Iluminacao, "Iluminação"),
        (TipoDeCarga.TUG, "TUG"),
        (TipoDeCarga.TUE, "TUE"),
        (TipoDeCarga.ArCondicionado, "Ar-condicionado"),
        (TipoDeCarga.Motor, "Motor"),
        (TipoDeCarga.Reserva, "Reserva")
    ];

    public string Titulo { get; }

    public string Dica { get; }

    public IReadOnlyList<LinhaDeFator> Linhas { get; }

    public string Problemas
    {
        get => _problemas;
        private set => SetProperty(ref _problemas, value);
    }

    /// <summary>Fatores interpretados, depois de <see cref="Confirmar" /> retornar <c>true</c>.</summary>
    public IReadOnlyDictionary<TipoDeCarga, decimal>? FatoresInterpretados { get; private set; }

    /// <summary>Interpreta os fatores; em caso de problema, mostra-os e mantém o diálogo aberto.</summary>
    public bool Confirmar()
    {
        var interpretados = new Dictionary<TipoDeCarga, decimal>();
        var problemas = new List<string>();
        foreach (var linha in Linhas)
        {
            var numero = NumeroDigitado.Interpretar(linha.Texto, CultureInfo.CurrentCulture);
            if (numero.Problema is { } problema)
            {
                problemas.Add($"{linha.Nome}: {problema}");
                continue;
            }

            if (numero.Valor is not { } valor) continue;
            if (valor <= 0 || valor > 1m)
            {
                problemas.Add($"{linha.Nome}: fator fora do intervalo (0, 1]");
                continue;
            }

            interpretados[linha.Tipo] = valor;
        }

        Problemas = string.Join(Environment.NewLine, problemas);
        if (problemas.Count > 0) return false;

        FatoresInterpretados = interpretados;
        return true;
    }
}

/// <summary>Uma linha do diálogo: o tipo de carga e o fator digitado (vazio = usar o perfil).</summary>
public partial class LinhaDeFator(TipoDeCarga tipo, string nome) : ObservableObject
{
    private string _texto = string.Empty;

    public TipoDeCarga Tipo { get; } = tipo;

    public string Nome { get; } = nome;

    public string Texto
    {
        get => _texto;
        set => SetProperty(ref _texto, value);
    }
}
