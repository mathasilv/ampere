using Ampere.Core.Cargas;
using Ampere.Core.Normas;

namespace Ampere.Core.Dimensionamento;

/// <summary>Valores AMP_* de um ponto do circuito, como o adapter os lê (nulo = parâmetro vazio).</summary>
/// <param name="Local">Local do ponto no vocabulário da tabela de IDR do perfil (nulo = sem local).</param>
/// <param name="TipoDeCarga">AMP_TipoCarga do ponto (texto): precisa ser o mesmo do circuito.</param>
public sealed record DadosDoPonto(long Id, decimal? PotenciaVA, decimal? TensaoV, string? Fases, string? Local = null, string? TipoDeCarga = null);

/// <summary>Comprimento que o Revit calcula para o circuito, do quadro aos pontos, e o caminho que ele considerou.</summary>
/// <param name="Metros">Comprimento, em m (zero = o Revit não calculou).</param>
/// <param name="Caminho">Como o Revit mediu (ex.: "do quadro ao ponto mais distante"), registrado na memória.</param>
public sealed record ComprimentoDoRevit(decimal Metros, string Caminho);

/// <summary>Valores AMP_* de um circuito e dos seus pontos, como o adapter os lê (nulo = parâmetro vazio).</summary>
/// <param name="Id">Identificador do circuito no documento.</param>
/// <param name="Numero">AMP_NumeroCircuito.</param>
/// <param name="TipoDeCarga">AMP_TipoCarga (texto).</param>
/// <param name="ComprimentoM">AMP_ComprimentoRotaM, em m: informado pelo projetista, prevalece sobre o do Revit.</param>
/// <param name="MetodoDeInstalacao">AMP_MetodoInstalacao.</param>
/// <param name="Isolacao">AMP_MaterialIsolacao.</param>
/// <param name="Pontos">Pontos de carga do circuito.</param>
/// <param name="TipoDeCondutor">AMP_TipoCondutor.</param>
/// <param name="Decisoes">Decisões do projetista no circuito (AMP_* de entrada), se houver.</param>
/// <param name="ComprimentoNoRevit">Comprimento calculado pelo Revit, usado quando AMP_ComprimentoRotaM está vazio.</param>
/// <param name="Quadro">AMP_Quadro do circuito (só para identificar o circuito nos relatórios e no resumo).</param>
/// <param name="ProblemasDeLeitura">Valores que o adapter não conseguiu ler (ex.: número fora da faixa): problemas de dados do circuito.</param>
public sealed record DadosDoCircuito(
    long Id,
    string? Numero,
    string? TipoDeCarga,
    decimal? ComprimentoM,
    string? MetodoDeInstalacao,
    string? Isolacao,
    IReadOnlyList<DadosDoPonto> Pontos,
    string? TipoDeCondutor = null,
    DecisoesDoProjetista? Decisoes = null,
    ComprimentoDoRevit? ComprimentoNoRevit = null,
    string? Quadro = null,
    IReadOnlyList<string>? ProblemasDeLeitura = null);

/// <summary>
///     Decisões do projetista no circuito, como o adapter as lê: parâmetros AMP_* de entrada, que o dimensionamento
///     nunca escreve (nulo = parâmetro vazio).
/// </summary>
/// <remarks>
///     Numérico 0 = sem decisão: o Revit não devolve parâmetro numérico a "sem valor", então zerar é o único jeito de o
///     projetista desfazer a decisão.
/// </remarks>
/// <param name="SecaoMinimaMm2">AMP_SecaoMinimaProjetistaMm2: piso da seção (o cálculo pode subir, nunca descer).</param>
/// <param name="DisjuntorA">AMP_DisjuntorProjetistaA: In fixa, verificada em IB ≤ In ≤ IZ.</param>
/// <param name="Idr">AMP_IDR_DecisaoProjetista: "Exigir" ou "Dispensar" (vazio = tabela por local).</param>
/// <param name="IdrSensibilidadeMa">AMP_IDR_SensibilidadeProjetistaMa: IΔn do IDR exigido.</param>
/// <param name="Justificativa">AMP_JustificativaProjetista, registrada na memória.</param>
/// <param name="TemperaturaAmbienteC">AMP_TemperaturaAmbienteC: no lugar da temperatura do projeto (em linha enterrada, a do solo).</param>
/// <param name="CircuitosAgrupados">AMP_CircuitosAgrupados: no lugar do agrupamento do projeto (inteiro, incluindo o circuito).</param>
public sealed record DecisoesDoProjetista(
    decimal? SecaoMinimaMm2 = null,
    decimal? DisjuntorA = null,
    string? Idr = null,
    decimal? IdrSensibilidadeMa = null,
    string? Justificativa = null,
    decimal? TemperaturaAmbienteC = null,
    decimal? CircuitosAgrupados = null)
{
    /// <summary>Valor de AMP_IDR_DecisaoProjetista que exige IDR no circuito.</summary>
    public const string ExigirIdr = "Exigir";

    /// <summary>Valor de AMP_IDR_DecisaoProjetista que dispensa o IDR do circuito.</summary>
    public const string DispensarIdr = "Dispensar";
}

/// <summary>
///     Condições do projeto que não são parâmetros do circuito, informadas pelo projetista.
/// </summary>
/// <param name="TemperaturaAmbienteC">Temperatura ambiente (do ar), em °C.</param>
/// <param name="CircuitosAgrupados">Circuitos agrupados (para o FCA).</param>
/// <param name="Material">Material dos condutores, ex.: Cobre.</param>
/// <param name="MetodoDeInstalacaoPadrao">Usado quando o circuito não tem AMP_MetodoInstalacao.</param>
/// <param name="IsolacaoPadrao">Usada quando o circuito não tem AMP_MaterialIsolacao.</param>
/// <param name="TipoDeCondutorPadrao">Usado quando o circuito não tem AMP_TipoCondutor; sem nenhum dos dois, o cálculo para no eletroduto.</param>
/// <param name="TipoDeEletroduto">Tipo de eletroduto do catálogo usado no projeto; sem ele, o cálculo para no eletroduto.</param>
/// <param name="TemperaturaDoSoloC">Temperatura do solo, em °C, para as linhas enterradas (método D); sem ela, o circuito enterrado precisa de AMP_TemperaturaAmbienteC.</param>
/// <param name="CircuitosAgrupadosNoSolo">Circuitos agrupados das linhas enterradas (a tabela é outra); sem ele, o circuito enterrado precisa de AMP_CircuitosAgrupados.</param>
/// <param name="CorrenteDeCurtoCircuitoKa">
///     Corrente de curto-circuito presumida, em kA (por cálculo ou medição, 5.3.5.1): a do ponto de entrada, a maior da
///     instalação, vale para todos os quadros a favor da segurança — os alimentadores também saem de quadros a montante.
///     Sem ela, a capacidade de interrupção e a integral de Joule não são verificadas.
/// </param>
public sealed record CondicoesDoProjeto(
    decimal TemperaturaAmbienteC,
    int CircuitosAgrupados,
    string Material,
    string? MetodoDeInstalacaoPadrao = null,
    string? IsolacaoPadrao = null,
    string? TipoDeCondutorPadrao = null,
    string? TipoDeEletroduto = null,
    decimal? TemperaturaDoSoloC = null,
    int? CircuitosAgrupadosNoSolo = null,
    decimal? CorrenteDeCurtoCircuitoKa = null);

/// <summary>
///     Monta a entrada do dimensionamento a partir dos dados do circuito: potência = soma dos pontos; tensão e fases =
///     o valor comum dos pontos que o informam (valores diferentes são problema, nunca um deles escolhido); comprimento =
///     AMP_ComprimentoRotaM ou, vazio, o calculado pelo Revit; decisões do projetista (seção mínima, disjuntor, IDR,
///     temperatura e agrupamento do circuito) passam ao motor, que as verifica e registra na memória.
/// </summary>
/// <remarks>
///     Comprimentos arredondados ao milímetro: a conversão de unidades do Revit (pés) deixa resíduo de ponto flutuante,
///     que mudaria o hash da memória sem mudar nada de engenharia. Tipos de condutor e de eletroduto são opcionais aqui:
///     sem eles, o motor calcula até o IDR e para no eletroduto, explicando o que falta.
/// </remarks>
public static class EntradaDoCircuito
{
    /// <param name="perfil">Diz se o método é de linha enterrada (a temperatura passa a ser a do solo).</param>
    public static EntradaMontada Montar(DadosDoCircuito dados, CondicoesDoProjeto condicoes, PerfilNormativo perfil)
    {
        var problemas = new List<string>(dados.ProblemasDeLeitura ?? []);
        if (string.IsNullOrWhiteSpace(dados.Numero)) problemas.Add("sem AMP_NumeroCircuito (rode 'Criar circuitos')");
        if (!CodigosDeTipoDeCarga.TryLer(dados.TipoDeCarga, out var tipo)) problemas.Add($"AMP_TipoCarga vazio ou desconhecido ('{dados.TipoDeCarga}')");
        var (comprimento, origemDoComprimento) = Comprimento(dados, problemas);
        var metodo = Preencher(dados.MetodoDeInstalacao, condicoes.MetodoDeInstalacaoPadrao, "AMP_MetodoInstalacao", problemas);
        var isolacao = Preencher(dados.Isolacao, condicoes.IsolacaoPadrao, "AMP_MaterialIsolacao", problemas);
        var tipoDeCondutor = Escolher(dados.TipoDeCondutor, condicoes.TipoDeCondutorPadrao);

        if (dados.Pontos.Count == 0) problemas.Add("circuito sem pontos");
        foreach (var ponto in dados.Pontos.Where(ponto => ponto.PotenciaVA is null))
            problemas.Add($"ponto {ponto.Id} sem AMP_PotenciaInstaladaVA");
        TiposDosPontos(dados, problemas);

        var tensoes = dados.Pontos.Select(ponto => ponto.TensaoV).OfType<decimal>().Distinct().Order().ToList();
        Unica(tensoes.Select(NumeroEmTexto.Formatar).ToList(), "AMP_TensaoCircuitoV", "tensões", problemas);
        var fases = dados.Pontos.Select(ponto => ponto.Fases).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        Unica(fases, "AMP_Fases", "fases", problemas);
        var decisoes = Decisoes(dados.Decisoes ?? new DecisoesDoProjetista(), condicoes, metodo is not null && perfil.Enterrado(metodo), problemas);
        if (problemas.Count > 0) return new EntradaMontada(null, problemas);

        var entrada = new EntradaDeDimensionamento(
            dados.Numero!,
            tipo,
            dados.Pontos.Sum(ponto => ponto.PotenciaVA!.Value),
            fases[0],
            tensoes[0],
            comprimento!.Value,
            metodo!,
            isolacao!,
            condicoes.Material,
            decisoes.TemperaturaAmbienteC,
            decisoes.CircuitosAgrupados,
            tipoDeCondutor,
            Escolher(condicoes.TipoDeEletroduto, null),
            dados.Pontos.Select(ponto => ponto.Local).ToList(),
            decisoes.Idr,
            origemDoComprimento,
            decisoes.SecaoMinimaMm2,
            decisoes.DisjuntorA,
            decisoes.Justificativa,
            decisoes.OrigemDaTemperatura,
            decisoes.OrigemDoAgrupamento,
            CorrenteDeCurtoCircuitoKa: condicoes.CorrenteDeCurtoCircuitoKa);
        return new EntradaMontada(entrada, []);
    }

    internal sealed record DecisoesMontadas(
        decimal? SecaoMinimaMm2,
        decimal? DisjuntorA,
        DecisaoDeIdr? Idr,
        string? Justificativa,
        decimal TemperaturaAmbienteC,
        string? OrigemDaTemperatura,
        int CircuitosAgrupados,
        string? OrigemDoAgrupamento);

    // Numérico 0 = sem decisão (o Revit não esvazia parâmetro numérico). Decisão incoerente é problema de dados, nunca
    // interpretada: o projetista corrige o parâmetro e roda de novo. Linha enterrada: a temperatura é a do solo e o
    // agrupamento é o das linhas enterradas — os do circuito ou os do projeto para o solo; sem nenhum dos dois, problema
    // (os do ar nunca valem para o solo).
    internal static DecisoesMontadas Decisoes(DecisoesDoProjetista decisoes, CondicoesDoProjeto condicoes, bool enterrado, List<string> problemas)
    {
        var justificativa = string.IsNullOrWhiteSpace(decisoes.Justificativa) ? null : decisoes.Justificativa.Trim();
        var secao = Positivo(decisoes.SecaoMinimaMm2, "AMP_SecaoMinimaProjetistaMm2", problemas);
        var disjuntor = Positivo(decisoes.DisjuntorA, "AMP_DisjuntorProjetistaA", problemas);
        var idr = Idr(decisoes, justificativa, problemas);
        var porque = justificativa is null ? "sem justificativa informada" : $"justificativa: {justificativa}";

        var (temperatura, origemDaTemperatura) = (condicoes.TemperaturaAmbienteC, (string?)null);
        if (!enterrado)
        {
            if (decisoes.TemperaturaAmbienteC is { } doCircuito and not 0m)
                (temperatura, origemDaTemperatura) = (doCircuito, $"AMP_TemperaturaAmbienteC do circuito (o projeto usa {NumeroEmTexto.Formatar(condicoes.TemperaturaAmbienteC)} °C; {porque})");
        }
        else if (decisoes.TemperaturaAmbienteC is { } doSolo and not 0m)
        {
            var doProjeto = condicoes.TemperaturaDoSoloC is { } solo ? $"o projeto usa {NumeroEmTexto.Formatar(solo)} °C no solo" : "o projeto não tem temperatura do solo";
            (temperatura, origemDaTemperatura) = (doSolo, $"AMP_TemperaturaAmbienteC do circuito, a do solo na linha enterrada ({doProjeto}; {porque})");
        }
        else if (condicoes.TemperaturaDoSoloC is { } solo)
        {
            (temperatura, origemDaTemperatura) = (solo, "temperatura do solo das condições do projeto (linha enterrada)");
        }
        else
        {
            problemas.Add("linha enterrada sem temperatura do solo: informe-a nas condições do projeto ou em AMP_TemperaturaAmbienteC do circuito");
        }

        var (agrupados, origemDoAgrupamento) = (condicoes.CircuitosAgrupados, (string?)null);
        if (decisoes.CircuitosAgrupados is { } informados and not 0m)
        {
            if (informados < 1m || informados > int.MaxValue || informados != decimal.Truncate(informados))
                problemas.Add($"AMP_CircuitosAgrupados deve ser um número inteiro de circuitos, pelo menos 1 ('{NumeroEmTexto.Formatar(informados)}'; 0 = o do projeto)");
            else if (!enterrado)
                (agrupados, origemDoAgrupamento) = ((int)informados,
                    $"AMP_CircuitosAgrupados do circuito (o projeto usa {condicoes.CircuitosAgrupados}; {porque})");
            else
                (agrupados, origemDoAgrupamento) = ((int)informados,
                    $"AMP_CircuitosAgrupados do circuito, na linha enterrada (" +
                    (condicoes.CircuitosAgrupadosNoSolo is { } noSolo ? $"o projeto usa {noSolo} no solo" : "o projeto não tem agrupamento no solo") + $"; {porque})");
        }
        else if (enterrado && condicoes.CircuitosAgrupadosNoSolo is { } noSolo)
        {
            (agrupados, origemDoAgrupamento) = (noSolo, "circuitos agrupados no solo das condições do projeto (linha enterrada)");
        }
        else if (enterrado)
        {
            problemas.Add("linha enterrada sem agrupamento no solo: informe os circuitos agrupados no solo nas condições do projeto ou AMP_CircuitosAgrupados no circuito");
        }

        return new DecisoesMontadas(secao, disjuntor, idr, justificativa, temperatura, origemDaTemperatura, agrupados, origemDoAgrupamento);
    }

    private static decimal? Positivo(decimal? valor, string parametro, List<string> problemas)
    {
        if (valor is not { } informado || informado == 0m) return null;
        if (informado < 0m) problemas.Add($"{parametro} negativo ('{NumeroEmTexto.Formatar(informado)}'; 0 = sem decisão)");
        return informado;
    }

    private static DecisaoDeIdr? Idr(DecisoesDoProjetista decisoes, string? justificativa, List<string> problemas)
    {
        const string Decisao = "AMP_IDR_DecisaoProjetista";
        const string Sensibilidade = "AMP_IDR_SensibilidadeProjetistaMa";
        var sensibilidade = decisoes.IdrSensibilidadeMa is { } valor and not 0m ? valor : (decimal?)null;
        var texto = decisoes.Idr?.Trim();

        if (string.IsNullOrEmpty(texto))
        {
            if (sensibilidade is not null) problemas.Add($"{Sensibilidade} preenchida sem {Decisao} = {DecisoesDoProjetista.ExigirIdr}");
            return null;
        }

        if (string.Equals(texto, DecisoesDoProjetista.DispensarIdr, StringComparison.OrdinalIgnoreCase))
        {
            if (sensibilidade is null) return DecisaoDeIdr.Dispensado(justificativa);
            problemas.Add($"{Decisao} = {DecisoesDoProjetista.DispensarIdr} com {Sensibilidade} preenchida: zere a sensibilidade ou exija o IDR");
            return null;
        }

        if (!string.Equals(texto, DecisoesDoProjetista.ExigirIdr, StringComparison.OrdinalIgnoreCase))
        {
            problemas.Add($"{Decisao} '{texto}' desconhecido: use {DecisoesDoProjetista.ExigirIdr} ou {DecisoesDoProjetista.DispensarIdr} (vazio = tabela por local)");
            return null;
        }

        switch (sensibilidade)
        {
            case null:
                problemas.Add($"{Decisao} = {DecisoesDoProjetista.ExigirIdr} sem {Sensibilidade}: informe a IΔn, em mA");
                return null;
            case < 0m:
                problemas.Add($"{Sensibilidade} negativa ('{NumeroEmTexto.Formatar(sensibilidade.Value)}'; 0 = sem decisão)");
                return null;
            default:
                return DecisaoDeIdr.Exigido(sensibilidade.Value, justificativa);
        }
    }

    // AMP_ComprimentoRotaM = 0 conta como vazio: o Revit não devolve parâmetro numérico a "sem valor", então zerar é o
    // único jeito de o projetista voltar ao comprimento do Revit — e L = 0 daria queda de tensão zero sem aviso.
    // Negativo segue como informado, para a validação da entrada recusá-lo.
    private static (decimal? Metros, string? Origem) Comprimento(DadosDoCircuito dados, List<string> problemas) =>
        Comprimento(dados.ComprimentoM, dados.ComprimentoNoRevit, problemas);

    internal static (decimal? Metros, string? Origem) Comprimento(decimal? comprimentoM, ComprimentoDoRevit? comprimentoNoRevit, List<string> problemas)
    {
        var informado = comprimentoM is { } valor ? Milimetro(valor) : (decimal?)null;
        if (informado is { } metros and not 0m) return (metros, "AMP_ComprimentoRotaM, informado pelo projetista");

        var zerado = informado is not null ? "AMP_ComprimentoRotaM = 0, tratado como vazio; " : string.Empty;
        if (comprimentoNoRevit is { } doRevit && Milimetro(doRevit.Metros) > 0)
            return (Milimetro(doRevit.Metros), $"{zerado}calculado pelo Revit ({doRevit.Caminho}), arredondado ao milímetro");

        var semInformado = informado is null ? "sem AMP_ComprimentoRotaM" : "AMP_ComprimentoRotaM = 0 (vazio)";
        problemas.Add(comprimentoNoRevit is null
            ? $"{semInformado} e sem comprimento do circuito no Revit"
            : $"{semInformado} e o Revit não calculou o comprimento do circuito (zero): informe o comprimento");
        return (null, null);
    }

    // O tipo do circuito decide a seção mínima e a exigência de IDR: ponto de outro tipo (reclassificado depois de
    // circuitado, ou posto no circuito pelo Revit) é problema, nunca um dos tipos escolhido.
    private static void TiposDosPontos(DadosDoCircuito dados, List<string> problemas)
    {
        if (!CodigosDeTipoDeCarga.TryLer(dados.TipoDeCarga, out var doCircuito)) return;

        var semTipo = dados.Pontos.Where(ponto => !CodigosDeTipoDeCarga.TryLer(ponto.TipoDeCarga, out _)).Select(ponto => ponto.Id).ToList();
        if (semTipo.Count > 0) problemas.Add($"ponto(s) sem AMP_TipoCarga reconhecido: {string.Join("; ", semTipo)}");

        var diferentes = dados.Pontos
            .Where(ponto => CodigosDeTipoDeCarga.TryLer(ponto.TipoDeCarga, out var doPonto) && doPonto != doCircuito)
            .GroupBy(ponto => { CodigosDeTipoDeCarga.TryLer(ponto.TipoDeCarga, out var doPonto); return CodigosDeTipoDeCarga.Codigo(doPonto); })
            .OrderBy(grupo => grupo.Key, StringComparer.Ordinal)
            .Select(grupo => $"{grupo.Key}: {grupo.Count()}")
            .ToList();
        if (diferentes.Count > 0)
            problemas.Add($"pontos de tipo diferente do circuito ({CodigosDeTipoDeCarga.Codigo(doCircuito)}) — {string.Join("; ", diferentes)}: corrija o AMP_TipoCarga ou refaça o circuito");
    }

    private static decimal Milimetro(decimal metros) => Math.Round(metros, 3, MidpointRounding.AwayFromZero);

    internal static string? Preencher(string? valor, string? padrao, string parametro, List<string> problemas)
    {
        var escolhido = Escolher(valor, padrao);
        if (escolhido is null) problemas.Add($"sem {parametro} nem padrão do projeto");
        return escolhido;
    }

    internal static string? Escolher(string? valor, string? padrao) =>
        !string.IsNullOrWhiteSpace(valor) ? valor : !string.IsNullOrWhiteSpace(padrao) ? padrao : null;

    // Lista com "; " porque a vírgula é o separador decimal dos números em texto.
    private static void Unica(IReadOnlyList<string> distintos, string parametro, string nome, List<string> problemas)
    {
        if (distintos.Count == 0) problemas.Add($"nenhum ponto com {parametro}");
        else if (distintos.Count > 1) problemas.Add($"pontos com {nome} diferentes ({string.Join("; ", distintos)})");
    }
}

/// <summary>Entrada pronta (nula se houver problema) e os problemas dos dados do circuito.</summary>
public sealed record EntradaMontada(EntradaDeDimensionamento? Entrada, IReadOnlyList<string> Problemas);
