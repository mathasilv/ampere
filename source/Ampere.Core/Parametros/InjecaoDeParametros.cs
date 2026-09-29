namespace Ampere.Core.Parametros;

/// <summary>
///     Caso de uso "Injetar parâmetros Ampere": planeja contra o documento e, só se não houver conflito, aplica
///     tudo numa única transação — um único passo de desfazer.
/// </summary>
public static class InjecaoDeParametros
{
    /// <summary>Nome da transação, que aparece no menu Desfazer do Revit.</summary>
    public const string NomeDaTransacao = "Ampere: injetar parâmetros";

    /// <returns>O plano: com conflitos, nada foi aplicado; sem conflitos, tudo foi.</returns>
    public static PlanoDeInjecao Executar(CatalogoDeParametros catalogo, IParametrosDoDocumento documento)
    {
        var plano = PlanejadorDeInjecao.Planejar(catalogo.Parametros, documento.LerExistentes());
        if (plano.TemConflitos || plano.NadaAFazer) return plano;

        var aCriar = plano.Acoes.OfType<AcaoDeInjecao.Criar>().Select(acao => acao.Definicao).ToList();
        var aAmpliar = plano.Acoes.OfType<AcaoDeInjecao.AmpliarCategorias>().ToList();

        documento.EmUmaTransacao(NomeDaTransacao, () =>
        {
            if (aCriar.Count > 0) documento.Criar(aCriar, catalogo.GrupoRevit);
            foreach (var ampliar in aAmpliar) documento.AmpliarCategorias(ampliar.Definicao, ampliar.Faltantes);
        });

        return plano;
    }
}
