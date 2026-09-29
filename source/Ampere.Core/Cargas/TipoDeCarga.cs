namespace Ampere.Core.Cargas;

/// <summary>
///     Tipo de carga de um ponto ou circuito (especificação §3, F1.2).
/// </summary>
public enum TipoDeCarga
{
    /// <summary>Iluminação.</summary>
    Iluminacao,

    /// <summary>Tomada de uso geral.</summary>
    TUG,

    /// <summary>Tomada de uso específico.</summary>
    TUE,

    /// <summary>Ar-condicionado.</summary>
    ArCondicionado,

    /// <summary>Motor.</summary>
    Motor,

    /// <summary>Circuito reserva — tipo de circuito, nunca de ponto de carga.</summary>
    Reserva
}
