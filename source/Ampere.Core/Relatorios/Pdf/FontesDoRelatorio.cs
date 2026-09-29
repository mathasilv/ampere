using System.Collections.Concurrent;
using PdfSharp.Fonts;

namespace Ampere.Core.Relatorios.Pdf;

/// <summary>
///     Fontes do relatório em PDF: DejaVu 2.35 embutida no assembly, com a mesma métrica em qualquer máquina e todos os
///     símbolos da memória (Δ, θ, ρ, Ω, ≤, ∈, √, ₀). Licença Bitstream Vera/DejaVu em Relatorios/Fontes/LICENSE_DEJAVU.
/// </summary>
/// <remarks>
///     O resolvedor de fontes do PDFsharp é global e só pode ser definido uma vez por processo. Se outro componente (outro
///     add-in no Revit) já tiver registrado o dele, o relatório falha com explicação, em vez de sair com fonte trocada.
/// </remarks>
internal sealed class FontesDoRelatorio : IFontResolver
{
    public const string Sans = "Ampere Sans";
    public const string Mono = "Ampere Mono";

    private static readonly FontesDoRelatorio Instancia = new();
    private static readonly object Trava = new();
    private readonly ConcurrentDictionary<string, byte[]> _arquivos = new(StringComparer.Ordinal);

    /// <exception cref="InvalidOperationException">Outro componente do processo já registrou um resolvedor de fontes.</exception>
    public static void Registrar()
    {
        lock (Trava)
        {
            var atual = GlobalFontSettings.FontResolver;
            if (ReferenceEquals(atual, Instancia)) return;
            if (atual is not null)
            {
                throw new InvalidOperationException(
                    $"Outro componente deste processo já registrou um resolvedor de fontes do PDFsharp ({atual.GetType().FullName}); " +
                    "o relatório em PDF do Ampere precisa do seu próprio para usar as fontes embutidas.");
            }

            GlobalFontSettings.FontResolver = Instancia;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) => familyName switch
    {
        Sans => new FontResolverInfo(bold ? "DejaVuSans-Bold" : "DejaVuSans", false, italic),
        Mono => new FontResolverInfo("DejaVuSansMono", bold, italic),
        _ => null
    };

    public byte[]? GetFont(string faceName) => _arquivos.GetOrAdd(faceName, Ler);

    private static byte[] Ler(string face)
    {
        var nome = $"Ampere.Core.Relatorios.Fontes.{face}.ttf";
        using var recurso = typeof(FontesDoRelatorio).Assembly.GetManifestResourceStream(nome)
                            ?? throw new InvalidOperationException($"Fonte embutida '{nome}' não encontrada.");
        using var memoria = new MemoryStream();
        recurso.CopyTo(memoria);
        return memoria.ToArray();
    }
}
