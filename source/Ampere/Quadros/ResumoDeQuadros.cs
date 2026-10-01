using System.Text;
using Ampere.Core;
using Ampere.Core.Normas;
using Ampere.Core.Quadros;

namespace Ampere.Quadros;

/// <summary>Textos apresentados ao fim da montagem dos quadros de cargas. Problemas repetidos são agrupados.</summary>
internal static class ResumoDeQuadros
{
    private const int MaximoDeLinhas = 8;

    public static string Montagem(
        IReadOnlyList<ResultadoDoQuadro> resultados, PerfilNormativo perfil, TimeSpan tempo,
        string? pastaDosRelatorios = null, int relatoriosGerados = 0, IReadOnlyList<string>? errosDeGravacao = null,
        int circuitosAtualizados = 0, IReadOnlyList<string>? tabelasCriadas = null, IReadOnlyList<string>? quadrosSemMemoria = null)
    {
        var texto = new StringBuilder();
        if (resultados.Count == 0)
        {
            texto.AppendLine("Nenhum quadro com circuitos no projeto.");
        }
        else
        {
            foreach (var resultado in resultados)
            {
                var quadro = resultado.Quadro;
                texto.AppendLine($" Quadro {resultado.Nome} ({quadro.Esquema})");
                texto.AppendLine($"   Circuitos: {quadro.Linhas.Count} · instalada: {NumeroEmTexto.Formatar(quadro.PotenciaInstaladaVA)} VA");
                if (quadro.DemandaVA is { } demanda)
                {
                    var corrente = quadro.CorrenteDeDemandaA is { } amperes ? $" · corrente: {NumeroEmTexto.Formatar(amperes)} A" : string.Empty;
                    texto.AppendLine($"   Demanda: {NumeroEmTexto.Formatar(demanda)} VA{corrente}");
                    if (quadro.Memoria is not null) texto.AppendLine($"   Memória: {quadro.Memoria.Hash()}");
                }
                else
                {
                    texto.AppendLine("   Demanda: incompleta (sem fator para algum tipo)");
                }

                if (resultado.Fases is { } fases)
                {
                    var cargas = string.Join(" · ", fases.Fases.Select(fase =>
                        $"{fase.Fase} {NumeroEmTexto.FormatarParaLeitura(fases.PelaDemanda ? fase.DemandaVA!.Value : fase.PotenciaInstaladaVA)} VA"
                        + (fase.CorrenteA is { } corrente ? $" ({NumeroEmTexto.FormatarParaLeitura(corrente)} A)" : string.Empty)));
                    texto.AppendLine($"   Fases ({(fases.PelaDemanda ? "demanda" : "instalada")}): {cargas} · desequilíbrio {NumeroEmTexto.FormatarParaLeitura(fases.DesequilibrioPct)}%");
                    if (fases.CircuitosSemFase.Count > 0) texto.AppendLine($"   Sem fase identificada: {string.Join(", ", fases.CircuitosSemFase)}");
                }

                if (resultado.Sugestao is { Mudancas.Count: > 0 } sugestao)
                {
                    var mudancas = string.Join("; ", sugestao.Mudancas.Take(MaximoDeLinhas).Select(mudanca =>
                        $"{mudanca.Numero} {(mudanca.Atuais is { } atuais ? string.Join(",", atuais) : "sem fase")} → {string.Join(",", mudanca.Sugeridas)}"));
                    if (sugestao.Mudancas.Count > MaximoDeLinhas) mudancas += "; …";
                    texto.AppendLine($"   Fases sugeridas: {mudancas} (maior corrente {NumeroEmTexto.FormatarParaLeitura(sugestao.CorrentesAntesA.Max())} → " +
                                     $"{NumeroEmTexto.FormatarParaLeitura(sugestao.CorrentesDepoisA.Max())} A; veja o relatório do quadro)");
                }
            }
        }

        var problemas = resultados.SelectMany(resultado => resultado.Quadro.Problemas.Select(problema => (Quadro: resultado.Nome, Problema: problema))).ToList();
        if (problemas.Count > 0)
        {
            texto.AppendLine();
            texto.AppendLine($"Problemas: {problemas.Count}");
            foreach (var grupo in problemas.GroupBy(problema => problema.Problema).OrderByDescending(grupo => grupo.Count()).Take(MaximoDeLinhas))
                texto.AppendLine($"• {grupo.Key} ({grupo.Count()})");
            if (problemas.GroupBy(problema => problema.Problema).Count() > MaximoDeLinhas) texto.AppendLine($"• … e mais");
        }

        if (circuitosAtualizados > 0) texto.AppendLine($"Circuitos atualizados: {circuitosAtualizados} (um único desfazer)").AppendLine();
        if (quadrosSemMemoria is { Count: > 0 })
            texto.AppendLine($"Memória não gravada no quadro (em grupo ou vínculo): {string.Join(", ", quadrosSemMemoria)}").AppendLine();

        if (tabelasCriadas is { Count: > 0 })
        {
            texto.AppendLine($"Tabelas criadas (navegador de projeto): {tabelasCriadas.Count}");
            foreach (var nome in tabelasCriadas) texto.AppendLine(nome);
            texto.AppendLine();
        }

        if (pastaDosRelatorios is { Length: > 0 })
        {
            texto.AppendLine();
            texto.AppendLine($"Relatórios gravados: {relatoriosGerados}");
            texto.AppendLine(pastaDosRelatorios);
        }

        if (errosDeGravacao is { Count: > 0 })
        {
            texto.AppendLine();
            texto.AppendLine("Erros de gravação:");
            foreach (var erro in errosDeGravacao.Take(MaximoDeLinhas)) texto.AppendLine($"• {erro}");
        }

        if (!string.IsNullOrEmpty(perfil.Nome)) texto.AppendLine().Append($"Perfil: {perfil.Nome}");
        texto.AppendLine().Append($"Tempo: {tempo.TotalSeconds:0.00} s");
        return texto.ToString();
    }
}
