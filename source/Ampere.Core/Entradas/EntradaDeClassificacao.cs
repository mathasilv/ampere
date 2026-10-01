using System.Globalization;
using Ampere.Core.Cargas;

namespace Ampere.Core.Entradas;

/// <summary>
///     Converte os campos do diálogo de classificação em <see cref="ClassificacaoDeCarga" />: primeiro a digitação, depois
///     as regras de domínio. Campo vazio = não alterar.
/// </summary>
/// <remarks>
///     A potência pode vir em VA ou, como na placa do equipamento, em W com o fator de potência: S = P / FP, arredondada
///     ao centésimo de VA. O que fica no ponto é sempre a potência aparente (AMP_PotenciaInstaladaVA).
/// </remarks>
public static class EntradaDeClassificacao
{
    /// <param name="local">Local escolhido (vazio = não alterar).</param>
    /// <param name="locaisDoPerfil">Locais da tabela de proteção diferencial do perfil: o local precisa ser um deles.</param>
    /// <param name="potenciaW">Potência ativa, em W (alternativa à potência em VA; exige o fator de potência).</param>
    /// <param name="aparelho">Aparelho do ponto TUE (código de AMP_Aparelho; vazio = não alterar).</param>
    public static ClassificacaoDigitada Interpretar(
        TipoDeCarga? tipo, string? potenciaVA, string? fatorDePotencia, string? tensaoV, string? fases, CultureInfo cultura,
        string? local = null, IReadOnlyCollection<string>? locaisDoPerfil = null, string? potenciaW = null, string? aparelho = null)
    {
        var problemas = new List<string>();
        if (tipo is null) problemas.Add("escolha o tipo de carga");
        var potencia = Campo("potência", potenciaVA, cultura, problemas);
        var potenciaAtiva = Campo("potência em W", potenciaW, cultura, problemas);
        var fator = Campo("fator de potência", fatorDePotencia, cultura, problemas);
        var tensao = Campo("tensão", tensaoV, cultura, problemas);
        if (potenciaAtiva is { } ativa)
        {
            if (potencia is not null) problemas.Add("informe a potência em VA ou em W, não as duas");
            else if (fator is null) problemas.Add("potência em W exige o fator de potência (VA = W / FP)");
            else if (fator is > 0m and <= 1m) potencia = EmVA(ativa, fator.Value, problemas);
            // Fator fora de (0; 1]: sem conversão; a regra de domínio recusa o fator com a mensagem dela.
        }

        var localEscolhido = string.IsNullOrWhiteSpace(local) ? null : local.Trim();
        if (localEscolhido is not null && locaisDoPerfil?.Contains(localEscolhido, StringComparer.Ordinal) != true)
            problemas.Add($"local '{localEscolhido}' fora da tabela de proteção diferencial do perfil");
        Aparelho? aparelhoEscolhido = null;
        if (!string.IsNullOrWhiteSpace(aparelho))
        {
            if (CodigosDeAparelho.TryLer(aparelho, out var lido)) aparelhoEscolhido = lido;
            else problemas.Add($"aparelho '{aparelho.Trim()}' desconhecido (use {string.Join(", ", CodigosDeAparelho.Todos)})");
        }

        if (problemas.Count > 0) return new ClassificacaoDigitada(null, problemas);

        var classificacao = new ClassificacaoDeCarga(tipo!.Value, potencia, fator, tensao, string.IsNullOrWhiteSpace(fases) ? null : fases.Trim(), localEscolhido,
            aparelhoEscolhido);
        var deDominio = classificacao.Validar();
        return deDominio.Count > 0 ? new ClassificacaoDigitada(null, deDominio) : new ClassificacaoDigitada(classificacao, []);
    }

    private static decimal? EmVA(decimal watts, decimal fatorDePotencia, List<string> problemas)
    {
        try
        {
            return Math.Round(watts / fatorDePotencia, 2, MidpointRounding.AwayFromZero);
        }
        catch (OverflowException)
        {
            problemas.Add("potência em W fora do intervalo (confira a potência e o fator de potência)");
            return null;
        }
    }

    private static decimal? Campo(string nome, string? texto, CultureInfo cultura, List<string> problemas)
    {
        var numero = NumeroDigitado.Interpretar(texto, cultura);
        if (numero.Problema is not null) problemas.Add($"{nome}: {numero.Problema}");
        return numero.Valor;
    }
}

/// <summary>Classificação pronta para aplicar (nula se houver problema) e os problemas encontrados.</summary>
public sealed record ClassificacaoDigitada(ClassificacaoDeCarga? Classificacao, IReadOnlyList<string> Problemas);
