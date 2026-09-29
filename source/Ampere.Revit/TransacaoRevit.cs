namespace Ampere.Revit;

/// <summary>
///     Transação única por operação: sem commit confirmado, nada fica no modelo.
/// </summary>
internal static class TransacaoRevit
{
    /// <summary>
    ///     Executa <paramref name="acao" /> numa transação. Exceção dentro da ação desfaz tudo (o descarte sem commit
    ///     reverte); commit não confirmado pelo Revit vira exceção.
    /// </summary>
    public static void Executar(Document documento, string nome, Action acao)
    {
        using var transacao = new Transaction(documento, nome);
        transacao.Start();
        acao();
        var situacao = transacao.Commit();
        if (situacao != TransactionStatus.Committed)
            throw new InvalidOperationException($"O Revit não confirmou a transação '{nome}' ({situacao}).");
    }
}
