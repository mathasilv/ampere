namespace Ampere.Core.Quadros;

/// <summary>
///     Lê as fases de um rótulo de fase do Revit (ex.: "A", "A,B", "A B" ou "AB") com os rótulos configurados no projeto:
///     primeiro por partes separadas por qualquer caractere que não seja letra ou dígito; sem separador e com rótulos de
///     uma letra só, letra a letra. Parte que não é rótulo do projeto = rótulo não lido (nulo), nunca um palpite.
/// </summary>
public static class RotuloDeFases
{
    public static IReadOnlyList<string>? Ler(string rotulo, IReadOnlyList<string> rotulos)
    {
        var partes = Partes(rotulo);
        if (partes.Count > 0 && partes.All(parte => rotulos.Contains(parte, StringComparer.Ordinal))) return Unicas(partes);

        if (partes.Count == 1 && rotulos.All(fase => fase.Length == 1))
        {
            var letras = partes[0].Select(letra => letra.ToString()).ToList();
            if (letras.All(letra => rotulos.Contains(letra, StringComparer.Ordinal))) return Unicas(letras);
        }

        return null;
    }

    private static List<string> Partes(string rotulo)
    {
        var partes = new List<string>();
        var atual = new System.Text.StringBuilder();
        foreach (var caractere in rotulo)
        {
            if (char.IsLetterOrDigit(caractere))
            {
                atual.Append(caractere);
                continue;
            }

            if (atual.Length > 0) partes.Add(atual.ToString());
            atual.Clear();
        }

        if (atual.Length > 0) partes.Add(atual.ToString());
        return partes;
    }

    private static List<string> Unicas(IEnumerable<string> fases) => fases.Distinct(StringComparer.Ordinal).ToList();
}
