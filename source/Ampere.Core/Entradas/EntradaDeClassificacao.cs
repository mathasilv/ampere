using System.Globalization;
using Ampere.Core.Cargas;

namespace Ampere.Core.Entradas;

/// <summary>
///     Converte os campos do diálogo de classificação em <see cref="ClassificacaoDeCarga" />: primeiro a digitação, depois
///     as regras de domínio. Campo vazio = não alterar.
/// </summary>
public static class EntradaDeClassificacao
{
    public static ClassificacaoDigitada Interpretar(
        TipoDeCarga? tipo, string? potenciaVA, string? fatorDePotencia, string? tensaoV, string? fases, CultureInfo cultura)
    {
        var problemas = new List<string>();
        if (tipo is null) problemas.Add("escolha o tipo de carga");
        var potencia = Campo("potência", potenciaVA, cultura, problemas);
        var fator = Campo("fator de potência", fatorDePotencia, cultura, problemas);
        var tensao = Campo("tensão", tensaoV, cultura, problemas);
        if (problemas.Count > 0) return new ClassificacaoDigitada(null, problemas);

        var classificacao = new ClassificacaoDeCarga(tipo!.Value, potencia, fator, tensao, string.IsNullOrWhiteSpace(fases) ? null : fases.Trim());
        var deDominio = classificacao.Validar();
        return deDominio.Count > 0 ? new ClassificacaoDigitada(null, deDominio) : new ClassificacaoDigitada(classificacao, []);
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
