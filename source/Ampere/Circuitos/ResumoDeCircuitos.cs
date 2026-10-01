using System.Text;
using Ampere.Core.Cargas;
using Ampere.Core.Circuitos;
using Ampere.Core.Locais;

namespace Ampere.Circuitos;

/// <summary>
///     Textos apresentados ao fim da classificação e da criação de circuitos. Motivos repetidos são agrupados.
/// </summary>
internal static class ResumoDeCircuitos
{
    private const int MaximoDeLinhas = 8;

    public static string Classificacao(ResultadoDaClassificacao resultado)
    {
        var texto = new StringBuilder();
        if (resultado.Problemas.Count > 0)
        {
            texto.AppendLine("Nada foi alterado:");
            foreach (var problema in resultado.Problemas) texto.AppendLine($"• {problema}");
            return texto.ToString();
        }

        texto.AppendLine($"Classificados: {resultado.Classificados}");
        AnexarMotivos(texto, "Recusados", resultado.Recusados);
        return texto.ToString();
    }

    public static string Locais(ResultadoDosLocais resultado)
    {
        var texto = new StringBuilder();
        if (resultado.Problemas.Count > 0)
        {
            texto.AppendLine("Nada foi alterado:");
            foreach (var problema in resultado.Problemas) texto.AppendLine($"• {problema}");
            return texto.ToString();
        }

        texto.AppendLine($"Pontos com local gravado: {resultado.Gravados} (um único desfazer)");
        if (resultado.JaEstavam > 0) texto.AppendLine($"Já estavam com o local escolhido: {resultado.JaEstavam}");
        if (resultado.SemEscolha > 0) texto.AppendLine($"Em ambientes sem local escolhido (não alterados): {resultado.SemEscolha}");
        if (resultado.NaoEditaveis > 0) texto.AppendLine($"Em grupo ou vínculo (não alterados): {resultado.NaoEditaveis}");
        if (resultado.SemAmbiente > 0) texto.AppendLine($"Fora de qualquer ambiente: {resultado.SemAmbiente} — informe o local pelo 'Classificar cargas'");
        return texto.ToString();
    }

    public static string Criacao(PlanoDeCircuitos plano, TimeSpan tempo)
    {
        var texto = new StringBuilder();
        if (plano.Circuitos.Count == 0)
        {
            texto.AppendLine("Nenhum circuito criado.");
        }
        else
        {
            texto.AppendLine($"Circuitos criados: {plano.Circuitos.Count}");
            texto.AppendLine(string.Join(", ", plano.Circuitos.Select(circuito => circuito.Numero)));
        }

        AnexarMotivos(texto, "Ignorados", plano.Ignorados);
        if (plano.Avisos.Count > 0)
        {
            texto.AppendLine();
            texto.AppendLine("Avisos:");
            foreach (var aviso in plano.Avisos.Take(MaximoDeLinhas)) texto.AppendLine($"• {aviso}");
            if (plano.Avisos.Count > MaximoDeLinhas) texto.AppendLine($"• … e mais {plano.Avisos.Count - MaximoDeLinhas}");
        }

        texto.AppendLine();
        texto.Append($"Tempo: {tempo.TotalSeconds:0.00} s");
        return texto.ToString();
    }

    private static void AnexarMotivos(StringBuilder texto, string titulo, IReadOnlyList<PontoIgnorado> ignorados)
    {
        if (ignorados.Count == 0) return;

        texto.AppendLine();
        texto.AppendLine($"{titulo}: {ignorados.Count}");
        var motivos = ignorados.GroupBy(ignorado => ignorado.Motivo).OrderByDescending(grupo => grupo.Count()).ToList();
        foreach (var motivo in motivos.Take(MaximoDeLinhas)) texto.AppendLine($"• {motivo.Key} ({motivo.Count()})");
        if (motivos.Count > MaximoDeLinhas) texto.AppendLine($"• … e mais {motivos.Count - MaximoDeLinhas} motivo(s)");
    }
}
