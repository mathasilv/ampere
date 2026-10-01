using System.IO;
using System.Text;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;

namespace Ampere.Dimensionamento;

/// <summary>Texto apresentado ao fim do dimensionamento. Motivos repetidos entre circuitos são agrupados.</summary>
internal static class ResumoDoDimensionamento
{
    private const int MaximoDeLinhas = 8;

    public static string Texto(
        IReadOnlyList<ResultadoDoCircuito> resultados, PerfilNormativo perfil, TimeSpan tempo, GravacaoDosRelatorios gravacao)
    {
        var texto = new StringBuilder();
        var dimensionados = resultados.Count(resultado => resultado.Dimensionamento?.Situacao == SituacaoDoDimensionamento.Dimensionado);
        var interrompidos = resultados.Where(resultado => resultado.Dimensionamento?.Situacao == SituacaoDoDimensionamento.Interrompido).ToList();
        var naoCalculados = resultados.Where(resultado => resultado.Memoria is null).ToList();

        texto.AppendLine($"Circuitos: {resultados.Count} (um único desfazer)");
        texto.AppendLine($"   Dimensionados: {dimensionados}");
        texto.AppendLine($"   Interrompidos (a memória mostra onde parou): {interrompidos.Count}");
        var semProtecao = interrompidos.Count(resultado => !resultado.Dimensionamento!.IdrAvaliado);
        if (semProtecao > 0) texto.AppendLine($"      sem seção, disjuntor e IDR gravados (parou antes de completar o IDR): {semProtecao}");
        texto.AppendLine($"   Não calculados (resultados anteriores apagados): {naoCalculados.Count}");

        Agrupar(texto, "Onde o cálculo parou", interrompidos.SelectMany(resultado => resultado.Dimensionamento!.Problemas));
        Listar(texto, "O que falta nos circuitos não calculados", naoCalculados.Select(resultado =>
            $"{Identificar(resultado)}: {string.Join("; ", resultado.ProblemasDeDados.Count > 0 ? resultado.ProblemasDeDados : resultado.Dimensionamento?.Problemas ?? [])}"));
        Listar(texto, "Avisos", resultados.SelectMany(resultado =>
            (resultado.Dimensionamento?.Avisos ?? []).Select(aviso => $"{Identificar(resultado)}: {aviso}")));

        if (gravacao.Pasta is { Length: > 0 } pasta)
        {
            texto.AppendLine();
            texto.AppendLine($"Memórias gravadas (JSON, Markdown e PDF): {gravacao.Gerados}");
            if (gravacao.Planilha is { } planilha) texto.AppendLine($"Planilha dos circuitos: {Path.GetFileName(planilha)}");
            if (gravacao.Materiais is { } materiais)
            {
                texto.AppendLine($"Lista de materiais: {Path.GetFileName(materiais.Caminho)} ({materiais.Itens} itens"
                                 + (materiais.CircuitosFora > 0 ? $"; {materiais.CircuitosFora} circuito(s) fora da lista, com o motivo no fim da planilha)" : ")"));
            }
            texto.AppendLine(pasta);
        }

        Listar(texto, "Erros de gravação dos relatórios", gravacao.Erros);

        if (!string.IsNullOrEmpty(perfil.Nome)) texto.AppendLine().Append($"Perfil: {perfil.Nome}");
        texto.AppendLine().Append($"Tempo: {tempo.TotalSeconds:0.00} s");
        return texto.ToString();
    }

    private static string Identificar(ResultadoDoCircuito resultado)
    {
        var numero = string.IsNullOrWhiteSpace(resultado.Numero) ? $"circuito {resultado.Id}" : resultado.Numero;
        return string.IsNullOrWhiteSpace(resultado.Quadro) ? numero : $"{resultado.Quadro} {numero}";
    }

    private static void Agrupar(StringBuilder texto, string titulo, IEnumerable<string> motivos)
    {
        var grupos = motivos.GroupBy(motivo => motivo).OrderByDescending(grupo => grupo.Count()).ThenBy(grupo => grupo.Key, StringComparer.Ordinal).ToList();
        if (grupos.Count == 0) return;

        texto.AppendLine();
        texto.AppendLine($"{titulo}:");
        foreach (var grupo in grupos.Take(MaximoDeLinhas)) texto.AppendLine($"• {grupo.Key} ({grupo.Count()})");
        if (grupos.Count > MaximoDeLinhas) texto.AppendLine($"• … e mais {grupos.Count - MaximoDeLinhas} motivo(s)");
    }

    private static void Listar(StringBuilder texto, string titulo, IEnumerable<string> linhas)
    {
        var todas = linhas.ToList();
        if (todas.Count == 0) return;

        texto.AppendLine();
        texto.AppendLine($"{titulo}:");
        foreach (var linha in todas.Take(MaximoDeLinhas)) texto.AppendLine($"• {linha}");
        if (todas.Count > MaximoDeLinhas) texto.AppendLine($"• … e mais {todas.Count - MaximoDeLinhas}");
    }
}
