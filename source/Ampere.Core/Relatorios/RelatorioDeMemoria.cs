using System.Text;
using Ampere.Core.Memoria;

namespace Ampere.Core.Relatorios;

/// <summary>
///     Relatório legível da memória de cálculo de um circuito: cada passo com a referência que o justifica, a expressão,
///     os valores e o resultado.
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
    /// <param name="memoria">Memória do circuito.</param>
    /// <param name="identificadorGravado">
    ///     AMP_MemoriaCalculoId lido do elemento, se houver: o relatório avisa quando não confere com esta memória.
    /// </param>
    public static string Markdown(MemoriaDeCalculo memoria, string? identificadorGravado = null)
    {
        var conteudo = ConteudoDoRelatorio.De(memoria, identificadorGravado);
        var texto = new StringBuilder();
        void Linha(string linha = "") => texto.Append(linha).Append('\n');
        void Campo(Campo campo) => Linha($"- **{Escapar(campo.Rotulo)}:** {(campo.EhCodigo ? Codigo(campo.Texto) : Escapar(campo.Texto))}");

        Linha($"# {Escapar(conteudo.Titulo)}");
        Linha();
        foreach (var campo in conteudo.Cabecalho) Campo(campo);
        Linha();
        Linha($"> {Escapar(conteudo.Nota)}");
        Linha();
        Linha("## Passos");
        foreach (var passo in conteudo.Passos)
        {
            Linha();
            Linha($"### {Escapar(passo.Titulo)}");
            Linha();
            foreach (var campo in passo.Campos) Campo(campo);
        }

        return texto.ToString();
    }

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
