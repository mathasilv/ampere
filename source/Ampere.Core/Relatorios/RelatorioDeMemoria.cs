using System.Text;
using Ampere.Core.Catalogos;
using Ampere.Core.Memoria;
using Ampere.Core.Normas;

namespace Ampere.Core.Relatorios;

/// <summary>
///     Relatório legível da memória de cálculo de um circuito, em Markdown: cada passo com a referência que o justifica, a
///     expressão, os valores e o resultado.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Determinístico: a mesma memória gera o mesmo texto, byte a byte (quebras de linha LF, Unicode NFC, sem data).</item>
///         <item>Números com vírgula decimal, arredondados só para leitura; o documento JSON guarda os valores completos.</item>
///         <item>Textos vindos de dados (referências, observações, nomes) são escapados para não virar marcação.</item>
///         <item>O cabeçalho aponta o que impede o uso como memorial definitivo: cálculo interrompido, passos com referência
///         pendente (TODO_NORMA, TODO_CATALOGO), perfil fictício e identificador gravado que não confere.</item>
///     </list>
/// </remarks>
public static class RelatorioDeMemoria
{
    private const string NotaDeArredondamento =
        "> Valores arredondados só para leitura: até 4 casas decimais (abaixo de 1, quatro algarismos significativos). " +
        "O documento JSON da memória guarda os valores completos.";

    // Unidades de contagem que o motor escreve no plural.
    private static readonly Dictionary<string, string> Singulares = new(StringComparer.Ordinal)
    {
        ["condutores"] = "condutor",
        ["pontos"] = "ponto"
    };

    /// <param name="memoria">Memória do circuito.</param>
    /// <param name="identificadorGravado">
    ///     AMP_MemoriaCalculoId lido do elemento, se houver: o relatório avisa quando não confere com esta memória.
    /// </param>
    public static string Markdown(MemoriaDeCalculo memoria, string? identificadorGravado = null)
    {
        var texto = new StringBuilder();
        void Linha(string linha = "") => texto.Append(linha).Append('\n');

        var identificador = memoria.Hash();
        Linha($"# Memória de cálculo — circuito {Escapar(memoria.Circuito)}");
        Linha();
        Linha($"- **Perfil normativo:** {Escapar(memoria.PerfilNorma)}");
        Linha($"- **Identificador (AMP_MemoriaCalculoId):** {Codigo(identificador)}");
        Linha($"- **Esquema do documento:** {MemoriaDeCalculo.VersaoDoEsquema}");
        Linha($"- **Situação:** {Situacao(memoria)}");

        var pendentes = memoria.Passos
            .Select((passo, indice) => (Referencia: passo.Referencia.Trim(), Numero: indice + 1))
            .Where(passo => passo.Referencia is PerfilNormativo.TodoNorma or RegrasDeCatalogo.TodoCatalogo)
            .Select(passo => passo.Numero)
            .ToList();
        if (pendentes.Count > 0)
            Linha($"- **Referências pendentes (TODO_NORMA ou TODO_CATALOGO):** {Contagem(pendentes.Count, "passo", "passos")} ({string.Join(", ", pendentes)}), sem fonte oficial");
        if (memoria.PerfilNorma.StartsWith("FICTICIO", StringComparison.Ordinal))
            Linha("- **Atenção:** perfil fictício, só para testes");
        if (identificadorGravado is not null && identificadorGravado.Trim() != identificador)
            Linha($"- **Atenção:** o identificador gravado no elemento ({Codigo(identificadorGravado)}) não confere com esta memória");

        Linha();
        Linha(NotaDeArredondamento);
        Linha();
        Linha("## Passos");
        foreach (var (passo, indice) in memoria.Passos.Select((passo, indice) => (passo, indice)))
        {
            Linha();
            Linha($"### {indice + 1}. {Escapar(passo.Descricao)}");
            Linha();
            Linha($"- **Referência:** {Escapar(passo.Referencia)}");
            Linha($"- **Expressão:** {Codigo(passo.Expressao)}");
            if (passo.Valores.Count > 0)
                Linha($"- **Valores:** {string.Join("; ", passo.Valores.Select(valor => $"{Escapar(valor.Nome)} = {Quantidade(valor.Valor, valor.Unidade)}"))}");
            Linha($"- **Resultado:** {(passo.Resultado is { } resultado ? Quantidade(resultado, passo.Unidade) : "não calculado")}");
            if (passo.Observacao is not null) Linha($"- **Observação:** {Escapar(passo.Observacao)}");
        }

        return texto.ToString();
    }

    // Resultado nulo marca o passo em que o cálculo parou (PassoDeCalculo.Resultado).
    private static string Situacao(MemoriaDeCalculo memoria)
    {
        var parada = memoria.Passos.Select((passo, indice) => (passo, indice)).FirstOrDefault(par => par.passo.Resultado is null);
        if (parada.passo is null) return $"cálculo completo ({Contagem(memoria.Passos.Count, "passo", "passos")})";

        return $"cálculo interrompido no passo {parada.indice + 1} ({Escapar(parada.passo.Descricao)}): " +
               Escapar(parada.passo.Observacao ?? "motivo não registrado");
    }

    private static string Quantidade(decimal valor, string unidade)
    {
        var numero = NumeroEmTexto.FormatarParaLeitura(valor);
        if (unidade.Length == 0) return numero;
        if (unidade == "%") return numero + "%";
        return $"{numero} {Escapar(valor == 1m && Singulares.TryGetValue(unidade, out var singular) ? singular : unidade)}";
    }

    private static string Contagem(int quantidade, string singular, string plural) => $"{quantidade} {(quantidade == 1 ? singular : plural)}";

    // Barra invertida antes de todo caractere que o Markdown pode interpretar. O sublinhado entre letras ou dígitos
    // (TODO_NORMA, AMP_TipoCarga) não abre ênfase e fica como está, para o texto continuar pesquisável.
    private static string Escapar(string texto)
    {
        var linha = UmaLinha(texto);
        var resultado = new StringBuilder(linha.Length);
        for (var indice = 0; indice < linha.Length; indice++)
        {
            var caractere = linha[indice];
            var escapar = caractere switch
            {
                '\\' or '`' or '*' or '[' or ']' or '<' or '>' or '|' or '~' or '&' => true,
                '_' => !(indice > 0 && char.IsLetterOrDigit(linha[indice - 1]) &&
                         indice + 1 < linha.Length && char.IsLetterOrDigit(linha[indice + 1])),
                _ => false
            };
            if (escapar) resultado.Append('\\');
            resultado.Append(caractere);
        }

        return resultado.ToString();
    }

    // Trecho de código: a cerca tem uma crase a mais que a maior sequência de crases do texto.
    private static string Codigo(string texto)
    {
        var linha = UmaLinha(texto);
        var maior = 0;
        var atual = 0;
        foreach (var caractere in linha)
        {
            atual = caractere == '`' ? atual + 1 : 0;
            maior = Math.Max(maior, atual);
        }

        var cerca = new string('`', maior + 1);
        var margem = linha.StartsWith('`') || linha.EndsWith('`') ? " " : string.Empty;
        return cerca + margem + linha + margem + cerca;
    }

    private static string UmaLinha(string texto) =>
        texto.Normalize(NormalizationForm.FormC).Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
}
