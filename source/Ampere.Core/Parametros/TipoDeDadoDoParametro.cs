namespace Ampere.Core.Parametros;

/// <summary>
///     Tipo de dado de um parâmetro Ampere. O código entre parênteses é o usado no arquivo de parâmetros.
/// </summary>
public enum TipoDeDadoDoParametro
{
    /// <summary>Texto (TEXT).</summary>
    Texto,

    /// <summary>Número adimensional (NUMBER).</summary>
    Numero,

    /// <summary>Potência aparente, em VA (ELECTRICAL_APPARENT_POWER).</summary>
    PotenciaAparente,

    /// <summary>Corrente elétrica, em A (CURRENT).</summary>
    Corrente,

    /// <summary>Comprimento (LENGTH).</summary>
    Comprimento
}
