namespace Ampere.Core.Dimensionamento;

/// <summary>
///     O que o dimensionamento grava nos AMP_* de resultado de um circuito (nulo = apagado), num lugar só: a gravação usa
///     <see cref="De" />, e a verificação compara com o que está no modelo para achar resultado editado à mão.
/// </summary>
/// <remarks>
///     Proteção (seção, I<sub>Z</sub>, disjuntor, IDR, queda e eletroduto) só com o IDR decidido por completo: sem isso, IDR
///     vazio ao lado de um disjuntor seria lido como "sem IDR". Sem memória (dados faltando, entrada inválida), tudo apagado.
/// </remarks>
public sealed record ResultadosNoCircuito(
    decimal? CorrenteDeProjetoA,
    decimal? FCA,
    decimal? FCT,
    decimal? SecaoMm2,
    decimal? CapacidadeDeConducaoA,
    decimal? DisjuntorA,
    decimal? IdrNominalA,
    decimal? IdrSensibilidadeMa,
    decimal? QuedaDeTensaoPct,
    string? Eletroduto,
    decimal? OcupacaoDoEletrodutoPct,
    string? PerfilNorma,
    string? MemoriaCalculoId)
{
    // Os números voltam do Revit como double: tolerância relativa bem abaixo de qualquer arredondamento de projeto.
    private const decimal Tolerancia = 0.000000001m;

    public static ResultadosNoCircuito De(ResultadoDoCircuito resultado)
    {
        var calculo = resultado.Memoria is null ? null : resultado.Dimensionamento;
        var protecao = calculo is { IdrAvaliado: true } ? calculo : null;
        return new ResultadosNoCircuito(
            calculo?.CorrenteDeProjetoA,
            calculo?.FCA,
            calculo?.FCT,
            protecao?.SecaoMm2,
            protecao?.CapacidadeDeConducaoA,
            protecao?.DisjuntorA,
            protecao?.IdrNominalA,
            protecao?.IdrSensibilidadeMa,
            protecao?.QuedaDeTensaoPct,
            protecao?.Eletroduto,
            protecao?.OcupacaoDoEletrodutoPct,
            calculo?.PerfilNorma,
            resultado.Memoria?.Hash());
    }

    /// <summary>
    ///     Os parâmetros em que este (o que está no modelo) difere do esperado, com os dois valores — ex.: "AMP_DisjuntorNominalA
    ///     = 25 no modelo; o cálculo dá 20".
    /// </summary>
    public IReadOnlyList<string> Diferencas(ResultadosNoCircuito esperados)
    {
        var diferencas = new List<string>();
        Numero("AMP_CorrenteProjetoA", CorrenteDeProjetoA, esperados.CorrenteDeProjetoA, diferencas);
        Numero("AMP_FCA", FCA, esperados.FCA, diferencas);
        Numero("AMP_FCT", FCT, esperados.FCT, diferencas);
        Numero("AMP_BitolaCondutorMm2", SecaoMm2, esperados.SecaoMm2, diferencas);
        Numero("AMP_CapacidadeConducaoA", CapacidadeDeConducaoA, esperados.CapacidadeDeConducaoA, diferencas);
        Numero("AMP_DisjuntorNominalA", DisjuntorA, esperados.DisjuntorA, diferencas);
        Numero("AMP_IDR_NominalA", IdrNominalA, esperados.IdrNominalA, diferencas);
        Numero("AMP_IDR_SensibilidadeMa", IdrSensibilidadeMa, esperados.IdrSensibilidadeMa, diferencas);
        Numero("AMP_QuedaTensaoPct", QuedaDeTensaoPct, esperados.QuedaDeTensaoPct, diferencas);
        Texto("AMP_EletrodutoTipo", Eletroduto, esperados.Eletroduto, diferencas);
        Numero("AMP_OcupacaoEletrodutoPct", OcupacaoDoEletrodutoPct, esperados.OcupacaoDoEletrodutoPct, diferencas);
        Texto("AMP_PerfilNorma", PerfilNorma, esperados.PerfilNorma, diferencas);
        return diferencas;
    }

    private static void Numero(string parametro, decimal? noModelo, decimal? esperado, List<string> diferencas)
    {
        var igual = (noModelo, esperado) switch
        {
            (null, null) => true,
            ({ } modelo, { } calculado) => Math.Abs(modelo - calculado) <= Tolerancia * Math.Max(1m, Math.Abs(calculado)),
            _ => false
        };
        if (!igual) diferencas.Add($"{parametro} = {Valor(noModelo)} no modelo; o cálculo dá {Valor(esperado)}");
    }

    private static void Texto(string parametro, string? noModelo, string? esperado, List<string> diferencas)
    {
        var modelo = string.IsNullOrWhiteSpace(noModelo) ? null : noModelo.Trim();
        var calculado = string.IsNullOrWhiteSpace(esperado) ? null : esperado.Trim();
        if (!string.Equals(modelo, calculado, StringComparison.Ordinal))
            diferencas.Add($"{parametro} = {modelo ?? "vazio"} no modelo; o cálculo dá {calculado ?? "vazio"}");
    }

    private static string Valor(decimal? valor) => valor is { } numero ? NumeroEmTexto.FormatarParaLeitura(numero) : "vazio";
}
