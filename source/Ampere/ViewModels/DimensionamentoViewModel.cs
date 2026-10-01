using System.Globalization;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Entradas;
using Ampere.Core.Normas;

namespace Ampere.ViewModels;

/// <summary>
///     Diálogo "Dimensionar circuitos": condições do projeto que não são parâmetros do circuito. Métodos, isolações e
///     materiais vêm do perfil; tipos de condutor e de eletroduto, dos catálogos (editáveis enquanto os catálogos estão
///     vazios). A interpretação e a validação são do Core.
/// </summary>
public sealed class DimensionamentoViewModel : ObservableObject
{
    private string _temperaturaC = string.Empty;
    private string _temperaturaDoSoloC = string.Empty;
    private string _circuitosAgrupados = string.Empty;
    private string _circuitosAgrupadosNoSolo = string.Empty;
    private string _correnteDeCurtoCircuitoKa = string.Empty;
    private string _material;
    private string _metodoPadrao = string.Empty;
    private string _isolacaoPadrao = string.Empty;
    private string _tipoDeCondutorPadrao = string.Empty;
    private string _tipoDeEletroduto = string.Empty;
    private string _problemas = string.Empty;

    /// <param name="daSelecao">Os circuitos vêm da seleção, não do projeto todo.</param>
    /// <param name="anteriores">Condições da rodada anterior nesta sessão do Revit, para não digitar tudo de novo.</param>
    public DimensionamentoViewModel(
        int quantidadeDeCircuitos, bool daSelecao, VocabularioDoPerfil vocabulario, IReadOnlyList<string> tiposDeCondutor, IReadOnlyList<string> tiposDeEletroduto,
        CondicoesDoProjeto? anteriores)
    {
        Titulo = daSelecao
            ? $"{quantidadeDeCircuitos} circuito(s) do Ampere na seleção (o restante do projeto e as condições guardadas do projeto não mudam)"
            : $"{quantidadeDeCircuitos} circuito(s) do Ampere no projeto";
        Materiais = vocabulario.Materiais;
        MetodosDeInstalacao = [string.Empty, .. vocabulario.MetodosDeInstalacao];
        Isolacoes = [string.Empty, .. vocabulario.Isolacoes];
        TiposDeCondutor = tiposDeCondutor;
        TiposDeEletroduto = tiposDeEletroduto;
        AvisoDeCatalogo = tiposDeCondutor.Count == 0 || tiposDeEletroduto.Count == 0
            ? "Catálogos de condutores e eletrodutos ainda sem dados (GAP-004): o cálculo vai até o IDR e para no eletroduto."
            : string.Empty;
        _material = Materiais.FirstOrDefault() ?? string.Empty;

        if (anteriores is null) return;
        // Na cultura atual, a mesma que NumeroDigitado usa para ler o campo de volta.
        _temperaturaC = anteriores.TemperaturaAmbienteC.ToString(CultureInfo.CurrentCulture);
        _temperaturaDoSoloC = anteriores.TemperaturaDoSoloC?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        _circuitosAgrupados = anteriores.CircuitosAgrupados.ToString(CultureInfo.CurrentCulture);
        _circuitosAgrupadosNoSolo = anteriores.CircuitosAgrupadosNoSolo?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        _correnteDeCurtoCircuitoKa = anteriores.CorrenteDeCurtoCircuitoKa?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        _material = anteriores.Material;
        _metodoPadrao = anteriores.MetodoDeInstalacaoPadrao ?? string.Empty;
        _isolacaoPadrao = anteriores.IsolacaoPadrao ?? string.Empty;
        _tipoDeCondutorPadrao = anteriores.TipoDeCondutorPadrao ?? string.Empty;
        _tipoDeEletroduto = anteriores.TipoDeEletroduto ?? string.Empty;
    }

    public string Titulo { get; }

    public string Dica { get; } =
        $"Padrões em branco: cada circuito usa os seus AMP_MetodoInstalacao, AMP_MaterialIsolacao e AMP_TipoCondutor. Comprimento: AMP_ComprimentoRotaM ou, vazio, o do circuito no Revit. "
        + "Temperatura e agrupamento: os do circuito (AMP_TemperaturaAmbienteC, AMP_CircuitosAgrupados) prevalecem — na linha enterrada (D), são os do solo; decisões do projetista (seção mínima, disjuntor, IDR) também ficam no circuito — 0 = sem decisão. "
        + $"Decimais com '{CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator}'.";

    public string AvisoDeCatalogo { get; }

    public IReadOnlyList<string> Materiais { get; }

    public IReadOnlyList<string> MetodosDeInstalacao { get; }

    public IReadOnlyList<string> Isolacoes { get; }

    public IReadOnlyList<string> TiposDeCondutor { get; }

    public IReadOnlyList<string> TiposDeEletroduto { get; }

    public string TemperaturaC
    {
        get => _temperaturaC;
        set => SetProperty(ref _temperaturaC, value);
    }

    public string TemperaturaDoSoloC
    {
        get => _temperaturaDoSoloC;
        set => SetProperty(ref _temperaturaDoSoloC, value);
    }

    public string CircuitosAgrupados
    {
        get => _circuitosAgrupados;
        set => SetProperty(ref _circuitosAgrupados, value);
    }

    public string CircuitosAgrupadosNoSolo
    {
        get => _circuitosAgrupadosNoSolo;
        set => SetProperty(ref _circuitosAgrupadosNoSolo, value);
    }

    public string CorrenteDeCurtoCircuitoKa
    {
        get => _correnteDeCurtoCircuitoKa;
        set => SetProperty(ref _correnteDeCurtoCircuitoKa, value);
    }

    public string Material
    {
        get => _material;
        set => SetProperty(ref _material, value);
    }

    public string MetodoPadrao
    {
        get => _metodoPadrao;
        set => SetProperty(ref _metodoPadrao, value);
    }

    public string IsolacaoPadrao
    {
        get => _isolacaoPadrao;
        set => SetProperty(ref _isolacaoPadrao, value);
    }

    public string TipoDeCondutorPadrao
    {
        get => _tipoDeCondutorPadrao;
        set => SetProperty(ref _tipoDeCondutorPadrao, value);
    }

    public string TipoDeEletroduto
    {
        get => _tipoDeEletroduto;
        set => SetProperty(ref _tipoDeEletroduto, value);
    }

    public string Problemas
    {
        get => _problemas;
        private set => SetProperty(ref _problemas, value);
    }

    /// <summary>Condições prontas, depois de <see cref="Confirmar" /> retornar <c>true</c>.</summary>
    public CondicoesDoProjeto? Condicoes { get; private set; }

    /// <summary>Interpreta os campos; em caso de problema, mostra-os e mantém o diálogo aberto.</summary>
    public bool Confirmar()
    {
        var entrada = EntradaDeCondicoes.Interpretar(
            TemperaturaC, TemperaturaDoSoloC, CircuitosAgrupados, CircuitosAgrupadosNoSolo, Material, MetodoPadrao, IsolacaoPadrao, TipoDeCondutorPadrao, TipoDeEletroduto, CultureInfo.CurrentCulture,
            CorrenteDeCurtoCircuitoKa);
        Condicoes = entrada.Condicoes;
        Problemas = string.Join(Environment.NewLine, entrada.Problemas);
        return Condicoes is not null;
    }
}
