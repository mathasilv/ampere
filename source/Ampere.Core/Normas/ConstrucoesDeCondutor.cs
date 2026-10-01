namespace Ampere.Core.Normas;

/// <summary>
///     Construção do condutor, no vocabulário da NBR 5410 (Tabela 33): o catálogo diz a de cada tipo, e o perfil diz quais
///     cada método de referência admite.
/// </summary>
public static class ConstrucoesDeCondutor
{
    public const string CondutorIsolado = "condutor isolado";
    public const string CaboUnipolar = "cabo unipolar";
    public const string CaboMultipolar = "cabo multipolar";

    public static IReadOnlyList<string> Todas { get; } = [CondutorIsolado, CaboUnipolar, CaboMultipolar];
}

/// <summary>O que o perfil diz de uma construção num método.</summary>
/// <param name="Condicao">Admitida só sob esta condição, que o modelo não mostra (vira aviso); nula = sem condição.</param>
/// <param name="Admitidas">As construções que o método admite sem condição, para a mensagem.</param>
public sealed record AdmissaoDaConstrucao(bool Admitida, string? Condicao, IReadOnlyList<string> Admitidas, string Referencia);
