using System.Globalization;
using Ampere.Core.Cargas;
using Ampere.Core.Entradas;
using Ampere.Core.Normas;

namespace Ampere.ViewModels;

/// <summary>
///     Diálogo "Classificar cargas". Os campos ficam em texto; a interpretação e a validação são do Core. Os locais são
///     os da tabela de proteção diferencial do perfil.
/// </summary>
public sealed class ClassificacaoViewModel(int quantidadeSelecionada, PerfilNormativo perfil) : ObservableObject
{
    private OpcaoDeTipo? _tipo;
    private string _local = string.Empty;
    private string _aparelho = string.Empty;
    private string _potenciaVA = string.Empty;
    private string _potenciaW = string.Empty;
    private string _fatorDePotencia = string.Empty;
    private string _tensaoV = string.Empty;
    private string _fases = string.Empty;
    private string _problemas = string.Empty;

    public string Titulo { get; } = $"{quantidadeSelecionada} elemento(s) selecionado(s)";

    public string Dica { get; } =
        $"Campos em branco não alteram o valor atual. Potência em VA, ou em W com o fator de potência (VA = W / FP). Decimais com '{CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator}', sem separador de milhar.";

    public IReadOnlyList<OpcaoDeTipo> Tipos { get; } = OpcaoDeTipo.ParaPontos();

    public IReadOnlyList<string> OpcoesDeFases { get; } = [string.Empty, .. ClassificacaoDeCarga.FasesValidas];

    /// <summary>Locais do perfil; o primeiro, vazio, não altera o local atual.</summary>
    public IReadOnlyList<string> OpcoesDeLocal { get; } = [string.Empty, .. perfil.Vocabulario.Locais];

    /// <summary>Aparelhos de AMP_Aparelho (só TUE); o primeiro, vazio, não altera o aparelho atual.</summary>
    public IReadOnlyList<string> OpcoesDeAparelho { get; } = [string.Empty, .. CodigosDeAparelho.Todos];

    public OpcaoDeTipo? Tipo
    {
        get => _tipo;
        set => SetProperty(ref _tipo, value);
    }

    public string PotenciaVA
    {
        get => _potenciaVA;
        set => SetProperty(ref _potenciaVA, value);
    }

    /// <summary>Potência ativa (W), alternativa à potência em VA: exige o fator de potência.</summary>
    public string PotenciaW
    {
        get => _potenciaW;
        set => SetProperty(ref _potenciaW, value);
    }

    public string FatorDePotencia
    {
        get => _fatorDePotencia;
        set => SetProperty(ref _fatorDePotencia, value);
    }

    public string TensaoV
    {
        get => _tensaoV;
        set => SetProperty(ref _tensaoV, value);
    }

    public string Fases
    {
        get => _fases;
        set => SetProperty(ref _fases, value);
    }

    public string Local
    {
        get => _local;
        set => SetProperty(ref _local, value);
    }

    public string Aparelho
    {
        get => _aparelho;
        set => SetProperty(ref _aparelho, value);
    }

    public string Problemas
    {
        get => _problemas;
        private set => SetProperty(ref _problemas, value);
    }

    /// <summary>Classificação pronta, depois de <see cref="Confirmar" /> retornar <c>true</c>.</summary>
    public ClassificacaoDeCarga? Classificacao { get; private set; }

    /// <summary>Interpreta os campos; em caso de problema, mostra-os e mantém o diálogo aberto.</summary>
    public bool Confirmar()
    {
        var entrada = EntradaDeClassificacao.Interpretar(
            Tipo?.Tipo, PotenciaVA, FatorDePotencia, TensaoV, Fases, CultureInfo.CurrentCulture, Local, perfil.Vocabulario.Locais, PotenciaW, Aparelho);
        Classificacao = entrada.Classificacao;
        Problemas = string.Join(Environment.NewLine, entrada.Problemas);
        return Classificacao is not null;
    }
}

/// <summary>Tipo de carga como opção de lista (Reserva não se aplica a pontos).</summary>
public sealed record OpcaoDeTipo(TipoDeCarga Tipo, string Nome)
{
    public static IReadOnlyList<OpcaoDeTipo> ParaPontos() =>
        Enum.GetValues<TipoDeCarga>()
            .Where(tipo => tipo != TipoDeCarga.Reserva)
            .Select(tipo => new OpcaoDeTipo(tipo, CodigosDeTipoDeCarga.Codigo(tipo)))
            .ToList();
}
