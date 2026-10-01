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
/// <param name="SecaoMinimaDoProjetistaMm2">Seção mínima decidida pelo projetista: piso do cálculo (a seção pode subir, nunca descer).</param>
/// <param name="DisjuntorDoProjetistaA">In decidida pelo projetista: fixa, verificada em IB ≤ In ≤ IZ (a seção sobe até atender).</param>
/// <param name="Justificativa">Justificativa das decisões do projetista, registrada na memória.</param>
/// <param name="OrigemDaTemperatura">De onde veio a temperatura quando não é a do projeto, registrado na memória.</param>
/// <param name="OrigemDoAgrupamento">De onde veio o número de circuitos agrupados quando não é o do projeto.</param>
/// <param name="OrigemDaPotencia">De onde veio a potência quando não é a soma dos pontos (ex.: demanda do quadro), registrado na memória.</param>
/// <param name="LimiteDeQueda">Limite de queda calculado fora da tabela do circuito terminal (ex.: o que sobra para o alimentador).</param>
/// <param name="Alimentador">Circuito que alimenta um quadro: a tabela de IDR por local não se aplica (só a decisão do projetista).</param>
/// <param name="CorrenteDeProjeto">I<sub>B</sub> calculada fora do motor (ex.: a da fase de maior corrente do quadro), no lugar de S / V.</param>
/// <param name="CorrenteDaQueda">Fator e corrente da queda de tensão calculados fora do motor (ex.: com o retorno pelo neutro), no lugar de k e I<sub>B</sub>.</param>
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
    string? OrigemDoComprimento = null,
    decimal? SecaoMinimaDoProjetistaMm2 = null,
    decimal? DisjuntorDoProjetistaA = null,
    string? Justificativa = null,
    string? OrigemDaTemperatura = null,
    string? OrigemDoAgrupamento = null,
    string? OrigemDaPotencia = null,
    LimiteDeQuedaDoCircuito? LimiteDeQueda = null,
    bool Alimentador = false,
    CorrenteCalculada? CorrenteDeProjeto = null,
    CorrenteDaQuedaDeTensao? CorrenteDaQueda = null)
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
        if (SecaoMinimaDoProjetistaMm2 is <= 0m) problemas.Add("seção mínima do projetista deve ser positiva");
        if (DisjuntorDoProjetistaA is <= 0m) problemas.Add("disjuntor do projetista deve ser positivo");
        if (LocaisDosPontos.Count == 0) problemas.Add("circuito sem pontos (locais dos pontos vazio)");
        if (LimiteDeQueda is { ValorPct: <= 0m }) problemas.Add("limite de queda de tensão deve ser positivo");
        if (CorrenteDeProjeto is { CorrenteA: < 0m }) problemas.Add("corrente de projeto não pode ser negativa");
        if (CorrenteDaQueda is { Fator: <= 0m } or { CorrenteA: < 0m }) problemas.Add("fator e corrente da queda de tensão devem ser positivos");
        if (IdrDoProjetista is { Exigir: true, SensibilidadeMa: not > 0 }) problemas.Add("IDR exigido pelo projetista sem sensibilidade positiva");
        if (IdrDoProjetista is { Exigir: false, SensibilidadeMa: not null }) problemas.Add("IDR dispensado pelo projetista não leva sensibilidade");
        return problemas;
    }
}

/// <summary>Limite de queda calculado fora da tabela do circuito terminal, com a conta para a memória.</summary>
/// <param name="ValorPct">ΔV% máximo do circuito.</param>
/// <param name="Referencia">Referência da tabela de queda do perfil de onde vêm os limites.</param>
/// <param name="Expressao">A conta (ex.: "ΔV%máx = ΔV%total − ΔV%terminal").</param>
public sealed record LimiteDeQuedaDoCircuito(decimal ValorPct, string Referencia, string Expressao, IReadOnlyList<ValorDoPasso> Valores, string? Observacao);

/// <summary>Corrente calculada fora do motor, com a conta para a memória.</summary>
/// <param name="Expressao">A conta (ex.: "IB = máx(I(A); I(B); I(C))").</param>
public sealed record CorrenteCalculada(decimal CorrenteA, string Expressao, IReadOnlyList<ValorDoPasso> Valores, string? Observacao);

/// <summary>
///     Base da queda de tensão calculada fora do motor: ΔV% = k · ρ · L · I<sub>ΔV</sub> / (S · V) · 100, com a conta de
///     I<sub>ΔV</sub> para a memória.
/// </summary>
/// <param name="Fator">k (2 nos circuitos de duas fases ou fase-neutro; √3 nos trifásicos equilibrados).</param>
/// <param name="CorrenteA">I<sub>ΔV</sub>.</param>
/// <param name="Expressao">A conta de I<sub>ΔV</sub> (ex.: "IΔV = IB + IN").</param>
/// <param name="Observacao">Por que esse k e essa corrente (vai para a memória).</param>
public sealed record CorrenteDaQuedaDeTensao(decimal Fator, decimal CorrenteA, string Expressao, IReadOnlyList<ValorDoPasso> Valores, string Observacao);

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
///     A proteção diferencial foi decidida por completo: a exigência (exigido ou não) e, se exigido, a corrente nominal do
///     IDR. Falso quando o cálculo parou antes ou dentro do IDR (inclusive sem corrente nominal de IDR que atenda o
///     disjuntor): aí IDR nulo não quer dizer "sem IDR", e o disjuntor não forma um conjunto de proteção completo.
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
