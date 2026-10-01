using Ampere.Core.Cargas;
using Ampere.Core.Memoria;

namespace Ampere.Core.Dimensionamento;

/// <summary>
///     Dados de um circuito para dimensionamento (vêm dos parâmetros AMP_* e das condições do projeto).
/// </summary>
/// <param name="Circuito">Número do circuito (AMP_NumeroCircuito).</param>
/// <param name="Tipo">Tipo de carga — define a seção mínima (iluminação ou força).</param>
/// <param name="PotenciaVA">Potência aparente instalada S, em VA (AMP_PotenciaInstaladaVA).</param>
/// <param name="Fases">Configuração (AMP_Fases): F+N, 2F, 2F+N, 3F ou 3F+N.</param>
/// <param name="TensaoV">Tensão do circuito (AMP_TensaoCircuitoV): fase-neutro em F+N, fase-fase nas demais.</param>
/// <param name="ComprimentoM">Comprimento do circuito, em m (AMP_ComprimentoRotaM).</param>
/// <param name="MetodoDeInstalacao">Método de instalação (AMP_MetodoInstalacao), ex.: B1.</param>
/// <param name="Isolacao">Isolação do condutor (AMP_MaterialIsolacao), ex.: PVC.</param>
/// <param name="Material">Material do condutor, ex.: Cobre.</param>
/// <param name="TemperaturaAmbienteC">Temperatura ambiente, em °C.</param>
/// <param name="CircuitosAgrupados">Circuitos agrupados com este (incluindo ele).</param>
/// <param name="TipoDeCondutor">Tipo de condutor no catálogo (AMP_TipoCondutor): dá o diâmetro externo. Nulo para o cálculo no eletroduto.</param>
/// <param name="TipoDeEletroduto">Tipo de eletroduto no catálogo: dá os tamanhos e diâmetros internos. Nulo para o cálculo no eletroduto.</param>
/// <param name="LocaisDosPontos">Local de cada ponto do circuito, no vocabulário da tabela de IDR do perfil (nulo = sem local).</param>
/// <param name="IdrDoProjetista">Decisão do projetista sobre o IDR; prevalece sobre a tabela por local.</param>
/// <param name="OrigemDoComprimento">De onde veio o comprimento (ex.: informado pelo projetista), registrado na memória.</param>
public sealed record EntradaDeDimensionamento(
    string Circuito,
    TipoDeCarga Tipo,
    decimal PotenciaVA,
    string Fases,
    decimal TensaoV,
    decimal ComprimentoM,
    string MetodoDeInstalacao,
    string Isolacao,
    string Material,
    decimal TemperaturaAmbienteC,
    int CircuitosAgrupados,
    string? TipoDeCondutor,
    string? TipoDeEletroduto,
    IReadOnlyList<string?> LocaisDosPontos,
    DecisaoDeIdr? IdrDoProjetista = null,
    string? OrigemDoComprimento = null)
{
    /// <summary>Problemas que impedem dimensionar; vazio se a entrada estiver válida.</summary>
    public IReadOnlyList<string> Validar()
    {
        var problemas = new List<string>();
        if (string.IsNullOrWhiteSpace(Circuito)) problemas.Add("circuito sem número");
        if (!Enum.IsDefined(Tipo)) problemas.Add("tipo de carga inválido");
        else if (Tipo == TipoDeCarga.Reserva) problemas.Add("circuito Reserva não é dimensionado");
        if (PotenciaVA < 0) problemas.Add("potência não pode ser negativa");
        if (!ClassificacaoDeCarga.FasesValidas.Contains(Fases)) problemas.Add($"fases inválidas: '{Fases}'");
        if (TensaoV <= 0) problemas.Add("tensão deve ser positiva");
        if (ComprimentoM <= 0) problemas.Add("comprimento deve ser positivo");
        if (string.IsNullOrWhiteSpace(MetodoDeInstalacao)) problemas.Add("método de instalação não informado");
        if (string.IsNullOrWhiteSpace(Isolacao)) problemas.Add("isolação não informada");
        if (string.IsNullOrWhiteSpace(Material)) problemas.Add("material do condutor não informado");
        if (CircuitosAgrupados < 1) problemas.Add("circuitos agrupados deve ser pelo menos 1");
        if (LocaisDosPontos.Count == 0) problemas.Add("circuito sem pontos (locais dos pontos vazio)");
        if (IdrDoProjetista is { Exigir: true, SensibilidadeMa: not > 0 }) problemas.Add("IDR exigido pelo projetista sem sensibilidade positiva");
        if (IdrDoProjetista is { Exigir: false, SensibilidadeMa: not null }) problemas.Add("IDR dispensado pelo projetista não leva sensibilidade");
        return problemas;
    }
}

/// <summary>
///     Decisão do projetista sobre o IDR de um circuito. Prevalece sobre a tabela de proteção diferencial por local; a
///     memória registra a decisão, o motivo e o que a tabela daria.
/// </summary>
/// <param name="Exigir"><c>true</c>: o circuito leva IDR; <c>false</c>: IDR dispensado.</param>
/// <param name="SensibilidadeMa">I<sub>Δn</sub> adotada, em mA: obrigatória ao exigir, ausente ao dispensar.</param>
/// <param name="Motivo">Justificativa do projetista, registrada na memória.</param>
public sealed record DecisaoDeIdr(bool Exigir, decimal? SensibilidadeMa, string? Motivo = null)
{
    public static DecisaoDeIdr Exigido(decimal sensibilidadeMa, string? motivo = null) => new(true, sensibilidadeMa, motivo);

    public static DecisaoDeIdr Dispensado(string? motivo = null) => new(false, null, motivo);
}

/// <summary>Resultado do dimensionamento de um circuito.</summary>
public enum SituacaoDoDimensionamento
{
    /// <summary>Todos os critérios atendidos.</summary>
    Dimensionado,

    /// <summary>O cálculo parou por falta de dado (ex.: tabela TODO_NORMA); a memória mostra onde e por quê.</summary>
    Interrompido,

    /// <summary>Entrada inválida: nada foi calculado.</summary>
    EntradaInvalida
}

/// <summary>
///     Resultado do dimensionamento: valores para os parâmetros AMP_* e a memória que os justifica. Valores ainda não
///     calculados quando o cálculo parou ficam nulos.
/// </summary>
/// <param name="IdrNominalA">Corrente nominal do IDR (AMP_IDR_NominalA); nula se o circuito não leva IDR.</param>
/// <param name="IdrSensibilidadeMa">I<sub>Δn</sub> do IDR (AMP_IDR_SensibilidadeMa); nula se o circuito não leva IDR.</param>
/// <param name="Eletroduto">Tamanho nominal adotado (AMP_EletrodutoTipo).</param>
/// <param name="DiametroInternoDoEletrodutoMm">Diâmetro interno do tamanho adotado, em mm.</param>
/// <param name="OcupacaoDoEletrodutoPct">Ocupação do eletroduto pelos condutores do circuito (AMP_OcupacaoEletrodutoPct).</param>
/// <param name="Problemas">Por que o cálculo não foi feito ou parou.</param>
/// <param name="Avisos">Divergências que não param o cálculo (ex.: decisão do projetista contrária à tabela).</param>
/// <param name="IdrAvaliado">
///     A exigência de IDR foi decidida (exigido ou não). Falso quando o cálculo parou antes ou na própria exigência: aí
///     IDR nulo não quer dizer "sem IDR", e o disjuntor não forma um conjunto de proteção completo.
/// </param>
public sealed record ResultadoDoDimensionamento(
    string Circuito,
    SituacaoDoDimensionamento Situacao,
    string PerfilNorma,
    decimal? CorrenteDeProjetoA,
    int? CondutoresCarregados,
    decimal? FCT,
    decimal? FCA,
    decimal? SecaoMm2,
    decimal? CapacidadeDeConducaoA,
    decimal? DisjuntorA,
    decimal? IdrNominalA,
    decimal? IdrSensibilidadeMa,
    decimal? QuedaDeTensaoPct,
    string? Eletroduto,
    decimal? DiametroInternoDoEletrodutoMm,
    decimal? OcupacaoDoEletrodutoPct,
    MemoriaDeCalculo? Memoria,
    IReadOnlyList<string> Problemas,
    IReadOnlyList<string> Avisos,
    bool IdrAvaliado = false);
