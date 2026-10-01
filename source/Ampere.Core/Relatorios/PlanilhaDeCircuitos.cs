using System.Text;
using Ampere.Core.Dimensionamento;

namespace Ampere.Core.Relatorios;

/// <summary>
///     Planilha (CSV) dos circuitos dimensionados, para abrir no Excel em português: separador ';', vírgula decimal e
///     números arredondados para leitura (o valor completo está na memória de cada circuito).
/// </summary>
/// <remarks>
///     Mostra o mesmo que o modelo: sem o IDR decidido por completo, as colunas de proteção ficam vazias, como os
///     parâmetros do circuito. Campo com ';', aspas ou quebra de linha vai entre aspas (RFC 4180).
/// </remarks>
public static class PlanilhaDeCircuitos
{
    private static readonly string[] Cabecalho =
    [
        "Quadro", "Circuito", "Tipo de carga", "Situação", "Potência (VA)", "IB (A)", "FCT", "FCA", "Seção (mm²)", "IZ (A)",
        "Disjuntor (A)", "IDR In (A)", "IDR IΔn (mA)", "Queda de tensão (%)", "Eletroduto", "Ocupação (%)", "Motivo", "Avisos",
        "Memória"
    ];

    public static string Csv(IReadOnlyList<ResultadoDoCircuito> resultados)
    {
        var texto = new StringBuilder();
        CsvEmPortugues.Linha(texto, Cabecalho);
        foreach (var resultado in resultados) CsvEmPortugues.Linha(texto, Colunas(resultado));
        return texto.ToString();
    }

    private static string?[] Colunas(ResultadoDoCircuito resultado)
    {
        var calculo = resultado.Memoria is null ? null : resultado.Dimensionamento;
        var protecao = calculo is { IdrAvaliado: true } ? calculo : null;
        var motivos = resultado.ProblemasDeDados.Count > 0 ? resultado.ProblemasDeDados : resultado.Dimensionamento?.Problemas ?? [];
        return
        [
            resultado.Quadro,
            resultado.Numero,
            resultado.TipoDeCarga,
            Situacao(resultado),
            Numero(resultado.PotenciaVA),
            Numero(calculo?.CorrenteDeProjetoA),
            Numero(calculo?.FCT),
            Numero(calculo?.FCA),
            Numero(protecao?.SecaoMm2),
            Numero(protecao?.CapacidadeDeConducaoA),
            Numero(protecao?.DisjuntorA),
            Numero(protecao?.IdrNominalA),
            Numero(protecao?.IdrSensibilidadeMa),
            Numero(protecao?.QuedaDeTensaoPct),
            protecao?.Eletroduto,
            Numero(protecao?.OcupacaoDoEletrodutoPct),
            string.Join(" | ", motivos),
            string.Join(" | ", resultado.Dimensionamento?.Avisos ?? []),
            resultado.Memoria?.Hash()
        ];
    }

    private static string Situacao(ResultadoDoCircuito resultado) => resultado.Dimensionamento?.Situacao switch
    {
        SituacaoDoDimensionamento.Dimensionado => "Dimensionado",
        SituacaoDoDimensionamento.Interrompido => resultado.Dimensionamento.IdrAvaliado ? "Interrompido" : "Interrompido antes da proteção",
        SituacaoDoDimensionamento.EntradaInvalida => "Entrada inválida",
        _ => "Não calculado (dados faltando)"
    };

    private static string? Numero(decimal? valor) => valor is { } numero ? NumeroEmTexto.FormatarParaLeitura(numero) : null;
}
