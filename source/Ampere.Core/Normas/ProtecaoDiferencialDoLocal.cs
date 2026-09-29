using Ampere.Core.Cargas;

namespace Ampere.Core.Normas;

/// <summary>
///     Exigência de proteção diferencial (IDR) de um local, como a tabela "protecao_diferencial_por_local" do perfil a
///     define. O vocabulário de locais é do perfil; quem classifica os ambientes do projeto usa esses nomes.
/// </summary>
/// <param name="Local">Nome do local na tabela.</param>
/// <param name="TiposDeCarga">Tipos de carga cujos circuitos exigem IDR neste local; vazio = o local não exige.</param>
/// <param name="SensibilidadeMaximaMa">I<sub>Δn</sub> máxima admitida, em mA; nula quando o local não exige.</param>
public sealed record ProtecaoDiferencialDoLocal(string Local, IReadOnlyList<TipoDeCarga> TiposDeCarga, decimal? SensibilidadeMaximaMa)
{
    /// <summary>I<sub>Δn</sub> máxima exigida para circuitos do tipo, ou nula se o local não exige IDR para ele.</summary>
    public decimal? SensibilidadeExigidaMa(TipoDeCarga tipo) => TiposDeCarga.Contains(tipo) ? SensibilidadeMaximaMa : null;
}
