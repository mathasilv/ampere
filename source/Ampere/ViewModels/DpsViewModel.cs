using Ampere.Core;
using Ampere.Core.Surtos;

namespace Ampere.ViewModels;

/// <summary>
///     Diálogo "DPS do quadro": o quadro (ponto de entrada ou quadro de distribuição principal), o esquema de aterramento, a
///     finalidade, a posição em relação ao DR e, quando a Figura 13 admite os dois, o esquema de conexão 2 ou 3. A seleção é
///     do Core.
/// </summary>
public sealed class DpsViewModel : ObservableObject
{
    private static readonly IReadOnlyDictionary<string, FinalidadeDoDps> TextosDasFinalidades = new Dictionary<string, FinalidadeDoDps>
    {
        ["Sobretensões transmitidas pela linha externa e de manobra"] = FinalidadeDoDps.LinhaExterna,
        ["Descargas atmosféricas diretas sobre a edificação ou nas proximidades"] = FinalidadeDoDps.DescargasDiretas,
        ["As duas"] = FinalidadeDoDps.Ambas
    };

    private readonly IReadOnlyList<QuadroParaDps> _quadros;
    private string _quadro;
    private string _aterramento;
    private string _finalidade;
    private bool _aJusanteDeDr;
    private string _esquemaDeConexao;
    private string _problemas = string.Empty;

    public DpsViewModel(IReadOnlyList<QuadroParaDps> quadros, string? quadroAnterior, EscolhaDoDps? anterior)
    {
        _quadros = quadros;
        Quadros = quadros.Select(Descrever).ToList();
        _quadro = quadros.FirstOrDefault(quadro => quadro.Nome == quadroAnterior) is { } lido ? Descrever(lido) : Quadros[0];
        _aterramento = anterior?.EsquemaDeAterramento is { } esquema && Aterramentos.Contains(esquema) ? esquema : EsquemasDeAterramento.TnCS;
        _finalidade = TextosDasFinalidades.FirstOrDefault(par => par.Value == anterior?.Finalidade).Key ?? Finalidades[0];
        _aJusanteDeDr = anterior?.AJusanteDeDr ?? false;
        _esquemaDeConexao = anterior?.EsquemaDeConexao == 3 ? EsquemasDeConexao[1] : EsquemasDeConexao[0];
    }

    public IReadOnlyList<string> Quadros { get; }

    public string Quadro
    {
        get => _quadro;
        set
        {
            if (SetProperty(ref _quadro, value)) AtualizarConexao();
        }
    }

    public IReadOnlyList<string> Aterramentos { get; } = EsquemasDeAterramento.Todos;

    public string Aterramento
    {
        get => _aterramento;
        set
        {
            if (SetProperty(ref _aterramento, value)) AtualizarConexao();
        }
    }

    public IReadOnlyList<string> Finalidades { get; } = TextosDasFinalidades.Keys.ToList();

    public string Finalidade
    {
        get => _finalidade;
        set => SetProperty(ref _finalidade, value);
    }

    public bool AJusanteDeDr
    {
        get => _aJusanteDeDr;
        set
        {
            if (SetProperty(ref _aJusanteDeDr, value)) AtualizarConexao();
        }
    }

    public IReadOnlyList<string> EsquemasDeConexao { get; } = ["Esquema 2 (fases e neutro ao PE)", "Esquema 3 (fases ao neutro, neutro ao PE)"];

    public string EsquemaDeConexao
    {
        get => _esquemaDeConexao;
        set => SetProperty(ref _esquemaDeConexao, value);
    }

    /// <summary>A Figura 13 admite os esquemas 2 e 3: a escolha é do projetista.</summary>
    public bool EscolheAConexao => SelecaoDeDps.EscolheOEsquemaDeConexao(QuadroEscolhido?.Esquema, Aterramento, AJusanteDeDr);

    /// <summary>O que decide o esquema de conexão, quando não é o projetista.</summary>
    public string Conexao => EscolheAConexao
        ? "A Figura 13 admite os esquemas 2 e 3: escolha um."
        : QuadroEscolhido?.Esquema is { } esquema && esquema.EndsWith("+N", StringComparison.Ordinal) && Aterramento == EsquemasDeAterramento.Tt
            ? "Esquema 3: TT com os DPS a montante do DR (6.3.5.2.6, alínea b)."
            : "Esquema 1: linha sem neutro, ou com o PEN aterrado no BEP (Figura 13).";

    public string Problemas
    {
        get => _problemas;
        private set => SetProperty(ref _problemas, value);
    }

    /// <summary>O quadro e a escolha, depois de <see cref="Confirmar" />.</summary>
    public (QuadroParaDps Quadro, EscolhaDoDps Escolha)? Resultado { get; private set; }

    private QuadroParaDps? QuadroEscolhido => _quadros.FirstOrDefault(quadro => Descrever(quadro) == Quadro);

    public bool Confirmar()
    {
        if (QuadroEscolhido is not { } quadro)
        {
            Problemas = "Escolha o quadro.";
            return false;
        }

        int? conexao = EscolheAConexao ? EsquemasDeConexao.ToList().IndexOf(EsquemaDeConexao) + 2 : null;
        Resultado = (quadro, new EscolhaDoDps(Aterramento, TextosDasFinalidades[Finalidade], conexao, AJusanteDeDr));
        return true;
    }

    private void AtualizarConexao()
    {
        OnPropertyChanged(nameof(EscolheAConexao));
        OnPropertyChanged(nameof(Conexao));
    }

    // Na lista: o nome e a alimentação (ex.: "QD1 — 3F+N 220/127 V").
    private static string Descrever(QuadroParaDps quadro)
    {
        if (quadro.Esquema is null) return $"{quadro.Nome} — sem sistema de distribuição";
        return $"{quadro.Nome} — {quadro.Esquema} {SelecaoDeDps.Tensoes(quadro)}";
    }
}
