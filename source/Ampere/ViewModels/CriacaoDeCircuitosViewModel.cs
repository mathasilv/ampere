using System.Globalization;
using Ampere.Core.Cargas;
using Ampere.Core.Circuitos;
using Ampere.Core.Entradas;

namespace Ampere.ViewModels;

/// <summary>
///     Diálogo "Criar circuitos": quadro, regras por tipo de carga (sem nenhum padrão normativo) e numeração.
/// </summary>
public sealed class CriacaoDeCircuitosViewModel : ObservableObject
{
    private QuadroEletrico? _quadro;
    private bool _incluirNomeDoQuadro;
    private string _problemas = string.Empty;

    public CriacaoDeCircuitosViewModel(int quantidadeSelecionada, IReadOnlyList<QuadroEletrico> quadros)
    {
        Titulo = $"{quantidadeSelecionada} elemento(s) selecionado(s)";
        Quadros = quadros;
        _quadro = quadros.FirstOrDefault();
        Regras = OpcaoDeTipo.ParaPontos()
            .Select(opcao => new LinhaDeRegra(opcao.Tipo, opcao.Nome, ConfiguracaoDeNumeracao.Padrao.Prefixos[opcao.Tipo]))
            .ToList();
    }

    public string Titulo { get; }

    public string Dica { get; } =
        $"Em branco = sem limite. Decimais com '{CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator}', sem separador de milhar.";

    public IReadOnlyList<QuadroEletrico> Quadros { get; }

    public IReadOnlyList<LinhaDeRegra> Regras { get; }

    public QuadroEletrico? Quadro
    {
        get => _quadro;
        set => SetProperty(ref _quadro, value);
    }

    public bool IncluirNomeDoQuadro
    {
        get => _incluirNomeDoQuadro;
        set => SetProperty(ref _incluirNomeDoQuadro, value);
    }

    public string Problemas
    {
        get => _problemas;
        private set => SetProperty(ref _problemas, value);
    }

    /// <summary>Regras interpretadas, depois de <see cref="Confirmar" /> retornar <c>true</c>.</summary>
    public IReadOnlyDictionary<TipoDeCarga, RegraDeAgrupamento>? RegrasInterpretadas { get; private set; }

    /// <summary>Numeração interpretada, depois de <see cref="Confirmar" /> retornar <c>true</c>.</summary>
    public ConfiguracaoDeNumeracao? Numeracao { get; private set; }

    /// <summary>Interpreta regras e numeração; em caso de problema, mostra-os e mantém o diálogo aberto.</summary>
    public bool Confirmar()
    {
        var problemas = new List<string>();
        if (Quadro is null) problemas.Add("escolha o quadro");

        var regras = new Dictionary<TipoDeCarga, RegraDeAgrupamento>();
        var prefixos = ConfiguracaoDeNumeracao.Padrao.Prefixos.ToDictionary();
        foreach (var linha in Regras)
        {
            var entrada = EntradaDeRegra.Interpretar(linha.Exclusivo, linha.MaximoDePontos, linha.MaximaPotenciaVA, CultureInfo.CurrentCulture);
            problemas.AddRange(entrada.Problemas.Select(problema => $"{linha.Nome}: {problema}"));
            if (entrada.Regra is { } regra) regras[linha.Tipo] = regra;
            prefixos[linha.Tipo] = linha.Prefixo.Trim();
        }

        var numeracao = ConfiguracaoDeNumeracao.Padrao with
        {
            Prefixos = prefixos,
            PrefixoDoQuadro = IncluirNomeDoQuadro ? Quadro?.Nome : null
        };
        problemas.AddRange(numeracao.Validar().Select(problema => $"numeração: {problema}"));

        Problemas = string.Join(Environment.NewLine, problemas);
        if (problemas.Count > 0) return false;

        RegrasInterpretadas = regras;
        Numeracao = numeracao;
        return true;
    }
}

/// <summary>Linha editável de regra e prefixo de um tipo de carga.</summary>
public sealed class LinhaDeRegra(TipoDeCarga tipo, string nome, string prefixo) : ObservableObject
{
    private string _prefixo = prefixo;
    private bool _exclusivo;
    private string _maximoDePontos = string.Empty;
    private string _maximaPotenciaVA = string.Empty;

    public TipoDeCarga Tipo { get; } = tipo;

    public string Nome { get; } = nome;

    public string Prefixo
    {
        get => _prefixo;
        set => SetProperty(ref _prefixo, value);
    }

    public bool Exclusivo
    {
        get => _exclusivo;
        set => SetProperty(ref _exclusivo, value);
    }

    public string MaximoDePontos
    {
        get => _maximoDePontos;
        set => SetProperty(ref _maximoDePontos, value);
    }

    public string MaximaPotenciaVA
    {
        get => _maximaPotenciaVA;
        set => SetProperty(ref _maximaPotenciaVA, value);
    }
}
