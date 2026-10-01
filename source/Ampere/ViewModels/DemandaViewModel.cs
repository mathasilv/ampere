using System.Globalization;
using Ampere.Core;
using Ampere.Core.Demanda;
using Ampere.Core.Entradas;

namespace Ampere.ViewModels;

/// <summary>
///     Diálogo "Demanda da entrada": a edificação (linha da tabela de iluminação e tomadas) e, com motores no modelo, a regra
///     deles — a do documento ou o fator do projetista com a justificativa. A validação do cálculo é do Core.
/// </summary>
public sealed class DemandaViewModel : ObservableObject
{
    private string _edificacao;
    private RegraDeMotores? _regra;
    private string _fatorDosMotores;
    private string _justificativa;
    private string _problemas = string.Empty;

    public DemandaViewModel(PerfilDeDemanda perfil, int motores, OpcoesDaDemanda? anteriores)
    {
        Titulo = $"Demanda pela {perfil.Nome}";
        Situacao = perfil.Situacao;
        Edificacoes = perfil.Edificacoes.Select(edificacao => edificacao.Nome).ToList();
        _edificacao = anteriores?.Edificacao is { } anterior && Edificacoes.Contains(anterior)
            ? anterior
            : perfil.Edificacoes.First(edificacao => edificacao.Residencial).Nome;
        TemMotores = motores > 0;
        Motores = motores > 0 ? $"{motores} motor(es) no modelo: escolha a regra" : "Sem motores no modelo.";
        RegraDoDocumentoTexto =
            $"Regra do documento ({perfil.Motores.Aplicacao}): {Fator(perfil.Motores.MaiorPct)} no maior e {Fator(perfil.Motores.DemaisPct)} nos demais";
        // Sem padrão: a regra do documento é só para um tipo de edificação, e a escolha é do projetista.
        _regra = anteriores?.Motores;
        _fatorDosMotores = anteriores?.FatorDosMotoresPct is { } fator ? fator.ToString(CultureInfo.CurrentCulture) : string.Empty;
        _justificativa = anteriores?.Justificativa ?? string.Empty;
    }

    public string Titulo { get; }

    /// <summary>Situação do documento da distribuidora (vigência), mostrada como aviso.</summary>
    public string Situacao { get; }

    public IReadOnlyList<string> Edificacoes { get; }

    public string Edificacao
    {
        get => _edificacao;
        set => SetProperty(ref _edificacao, value);
    }

    public bool TemMotores { get; }

    public string Motores { get; }

    public string RegraDoDocumentoTexto { get; }

    public bool RegraDoDocumento
    {
        get => _regra == RegraDeMotores.DoDocumento;
        set => Escolher(value, RegraDeMotores.DoDocumento);
    }

    public bool RegraDoProjetista
    {
        get => _regra == RegraDeMotores.DoProjetista;
        set => Escolher(value, RegraDeMotores.DoProjetista);
    }

    /// <summary>Fator dos motores em % (só com a regra do projetista).</summary>
    public string FatorDosMotores
    {
        get => _fatorDosMotores;
        set => SetProperty(ref _fatorDosMotores, value);
    }

    public string Justificativa
    {
        get => _justificativa;
        set => SetProperty(ref _justificativa, value);
    }

    public string Problemas
    {
        get => _problemas;
        private set => SetProperty(ref _problemas, value);
    }

    /// <summary>Opções prontas, depois de <see cref="Confirmar" /> retornar <c>true</c>.</summary>
    public OpcoesDaDemanda? Opcoes { get; private set; }

    public bool Confirmar()
    {
        Opcoes = null;
        if (!TemMotores)
        {
            Opcoes = new OpcoesDaDemanda(Edificacao);
            return true;
        }

        if (_regra is null)
        {
            Problemas = "escolha a regra dos motores";
            return false;
        }

        if (RegraDoDocumento)
        {
            Opcoes = new OpcoesDaDemanda(Edificacao, RegraDeMotores.DoDocumento);
            return true;
        }

        var problemas = new List<string>();
        var fator = NumeroDigitado.Interpretar(FatorDosMotores, CultureInfo.CurrentCulture);
        if (fator.Problema is not null) problemas.Add($"fator dos motores: {fator.Problema}");
        else if (fator.Valor is not (> 0m and <= 100m)) problemas.Add("fator dos motores: informe um valor entre 0 e 100%");
        if (string.IsNullOrWhiteSpace(Justificativa)) problemas.Add("justificativa do fator dos motores: obrigatória");
        Problemas = string.Join(Environment.NewLine, problemas);
        if (problemas.Count > 0) return false;

        Opcoes = new OpcoesDaDemanda(Edificacao, RegraDeMotores.DoProjetista, fator.Valor, Justificativa.Trim());
        return true;
    }

    private void Escolher(bool marcada, RegraDeMotores regra)
    {
        if (!marcada || _regra == regra) return;
        _regra = regra;
        OnPropertyChanged(nameof(RegraDoDocumento));
        OnPropertyChanged(nameof(RegraDoProjetista));
    }

    private static string Fator(decimal pct) => $"{NumeroEmTexto.Formatar(pct)}%";
}
