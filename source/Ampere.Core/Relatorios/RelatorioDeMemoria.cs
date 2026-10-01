using System.Text;
using Ampere.Core.Memoria;
using Ampere.Core.Quadros;
using Ampere.Core.Relatorios.Pdf;

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
    public static string Markdown(MemoriaDeCalculo memoria, string? identificadorGravado = null) =>
        Renderizar(ConteudoDoRelatorio.De(memoria, identificadorGravado));

    /// <summary>O relatório do quadro de cargas: circuitos e totais antes dos passos da memória.</summary>
    /// <exception cref="InvalidOperationException">Quadro incompleto (sem memória) não gera relatório.</exception>
    public static string MarkdownDoQuadro(ResultadoDoQuadroDeCargas quadro, BalancoDasFases? fases = null, SugestaoDeFases? sugestao = null) =>
        Renderizar(ConteudoDoRelatorio.DeQuadro(quadro, fases, sugestao));

    /// <summary>O relatório da demanda da entrada: documento da distribuidora e parcelas antes dos passos da memória.</summary>
    /// <exception cref="InvalidOperationException">Demanda sem cálculo não gera relatório.</exception>
    public static string MarkdownDaDemanda(Demanda.ResultadoDaDemanda demanda, Demanda.PerfilDeDemanda perfil) =>
        Renderizar(ConteudoDoRelatorio.DeDemanda(demanda, perfil));

    /// <summary>A demanda da entrada em PDF, com o identificador da memória como Assunto.</summary>
    /// <exception cref="InvalidOperationException">Demanda sem cálculo não gera relatório.</exception>
    public static byte[] PdfDaDemanda(Demanda.ResultadoDaDemanda demanda, Demanda.PerfilDeDemanda perfil) =>
        PdfDoRelatorio.Gerar(ConteudoDoRelatorio.DeDemanda(demanda, perfil), demanda.Memoria!.Hash());

    /// <summary>O relatório do padrão de entrada: documento da distribuidora e escolha antes dos passos da memória.</summary>
    /// <exception cref="InvalidOperationException">Padrão que não saiu não gera relatório.</exception>
    public static string MarkdownDoPadrao(Demanda.ResultadoDoPadrao padrao, Demanda.NormaDoPadraoDeEntrada norma) =>
        Renderizar(ConteudoDoRelatorio.DePadrao(padrao, norma));

    /// <summary>O padrão de entrada em PDF, com o identificador da memória como Assunto.</summary>
    /// <exception cref="InvalidOperationException">Padrão que não saiu não gera relatório.</exception>
    public static byte[] PdfDoPadrao(Demanda.ResultadoDoPadrao padrao, Demanda.NormaDoPadraoDeEntrada norma) =>
        PdfDoRelatorio.Gerar(ConteudoDoRelatorio.DePadrao(padrao, norma), padrao.Memoria!.Hash());

    private static string Renderizar(ConteudoDoRelatorio conteudo)
    {
        var texto = new StringBuilder();
        void Linha(string linha = "") => texto.Append(linha).Append('\n');
        void Campo(Campo campo) => Linha($"- **{Escapar(campo.Rotulo)}:** {(campo.EhCodigo ? Codigo(campo.Texto) : Escapar(campo.Texto))}");

        Linha($"# {Escapar(conteudo.Titulo)}");
        Linha();
        foreach (var campo in conteudo.Cabecalho) Campo(campo);
        Linha();
        Linha($"> {Escapar(conteudo.Nota)}");
        Linha();
        foreach (var secao in conteudo.Abertura)
        {
            Linha($"## {Escapar(secao.Titulo)}");
            Linha();
            foreach (var campo in secao.Campos) Campo(campo);
            Linha();
        }

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

    /// <summary>
    ///     O mesmo relatório em PDF A4, com as fontes embutidas no Ampere: a mesma memória gera o mesmo conteúdo, página
    ///     a página, em qualquer máquina. O Assunto do PDF leva o identificador da memória (AMP_MemoriaCalculoId).
    /// </summary>
    /// <exception cref="InvalidOperationException">Outro componente do processo já registrou um resolvedor de fontes do PDFsharp.</exception>
    public static byte[] Pdf(MemoriaDeCalculo memoria, string? identificadorGravado = null) =>
        PdfDoRelatorio.Gerar(ConteudoDoRelatorio.De(memoria, identificadorGravado), memoria.Hash());

    /// <summary>O quadro de cargas em PDF, com o identificador da memória como Assunto.</summary>
    /// <exception cref="InvalidOperationException">Quadro incompleto (sem memória) não gera relatório.</exception>
    public static byte[] PdfDoQuadro(ResultadoDoQuadroDeCargas quadro, BalancoDasFases? fases = null, SugestaoDeFases? sugestao = null) =>
        PdfDoRelatorio.Gerar(ConteudoDoRelatorio.DeQuadro(quadro, fases, sugestao), quadro.Memoria!.Hash());

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
