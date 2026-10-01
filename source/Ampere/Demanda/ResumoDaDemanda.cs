using System.Text;
using Ampere.Core;
using Ampere.Core.Demanda;

namespace Ampere.Demanda;

/// <summary>Texto apresentado ao fim do cálculo da demanda: o total, as parcelas e onde ficou a memória.</summary>
internal static class ResumoDaDemanda
{
    public static string Texto(ResultadoDaDemanda resultado, PerfilDeDemanda perfil, OpcoesDaDemanda opcoes, string? pasta, IReadOnlyList<string> erros)
    {
        var texto = new StringBuilder();
        texto.AppendLine($"{perfil.Nome} · {opcoes.Edificacao}");
        texto.AppendLine();
        texto.AppendLine($"Demanda: {Kva(resultado.DemandaVA!.Value)} kVA (instalada {Kva(resultado.PotenciaInstaladaVA)} kVA)");
        foreach (var parcela in resultado.Parcelas)
            texto.AppendLine($"• {parcela.Codigo} — {parcela.Descricao}: {Kva(parcela.DemandaVA)} kVA ({parcela.Pontos} ponto(s), instalada {Kva(parcela.PotenciaInstaladaVA)} kVA)");

        texto.AppendLine();
        texto.AppendLine("Vigência do documento a conferir (veja a memória).");
        if (pasta is { Length: > 0 })
        {
            texto.AppendLine("Memória (JSON, Markdown e PDF):");
            texto.AppendLine(pasta);
        }

        if (erros.Count > 0)
        {
            texto.AppendLine();
            texto.AppendLine("Erros de gravação dos relatórios:");
            foreach (var erro in erros.Take(8)) texto.AppendLine($"• {erro}");
        }

        return texto.ToString();
    }

    private static string Kva(decimal va) => NumeroEmTexto.FormatarParaLeitura(va / 1000m);
}
