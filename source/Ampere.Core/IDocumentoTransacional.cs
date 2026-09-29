namespace Ampere.Core;

/// <summary>
///     Base das portas para o documento: toda escrita acontece dentro de uma transação — um único passo de desfazer.
/// </summary>
public interface IDocumentoTransacional
{
    /// <summary>Executa <paramref name="acao" /> numa única transação.</summary>
    void EmUmaTransacao(string nome, Action acao);
}
