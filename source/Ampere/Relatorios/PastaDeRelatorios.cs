using System.IO;

namespace Ampere.Relatorios;

/// <summary>Onde os relatórios do projeto são gravados: Documentos\Ampere\{projeto}[\subpasta].</summary>
internal static class PastaDeRelatorios
{
    public static string Caminho(string nomeDoProjeto, string? subpasta = null)
    {
        var pasta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Ampere",
            string.IsNullOrWhiteSpace(nomeDoProjeto) ? "projeto-sem-titulo" : NomeDeArquivo(nomeDoProjeto));
        return subpasta is null ? pasta : Path.Combine(pasta, subpasta);
    }

    /// <summary>Nome válido de arquivo: caracteres proibidos viram '_'.</summary>
    public static string NomeDeArquivo(string nome)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var texto = new string(nome.Select(caractere => invalidos.Contains(caractere) ? '_' : caractere).ToArray());
        return texto.Trim();
    }

    /// <summary>Os 8 primeiros dígitos do hash ("sha256:…"), para distinguir versões da mesma memória no nome do arquivo.</summary>
    public static string Prefixo(string hash) => hash["sha256:".Length..("sha256:".Length + 8)];
}
