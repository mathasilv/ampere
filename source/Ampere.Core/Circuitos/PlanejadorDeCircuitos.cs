using System.Globalization;
using Ampere.Core.Cargas;

namespace Ampere.Core.Circuitos;

/// <summary>
///     Agrupa pontos de carga em circuitos e os numera. Função pura e determinística: mesmas entradas, em qualquer
///     ordem, dão o mesmo plano.
/// </summary>
/// <remarks>
///     Grupos por (tipo de carga, alimentação), na ordem do enum de tipos e da chave de alimentação; pontos por Id.
///     Divisão gulosa pelos limites da regra do tipo; um ponto que excede sozinho o limite de VA fica sozinho e gera
///     aviso. A numeração de cada prefixo continua depois do maior número já existente no quadro. Nada é descartado em
///     silêncio: todo ponto fora do plano aparece em <see cref="PlanoDeCircuitos.Ignorados" /> com o motivo.
/// </remarks>
public static class PlanejadorDeCircuitos
{
    private static readonly RegraDeAgrupamento SemLimites = new();

    /// <exception cref="ArgumentException">Alguma regra de agrupamento ou a numeração é inválida.</exception>
    public static PlanoDeCircuitos Planejar(
        IReadOnlyCollection<PontoDeCarga> pontos,
        IReadOnlyDictionary<TipoDeCarga, RegraDeAgrupamento> regras,
        ConfiguracaoDeNumeracao numeracao,
        IReadOnlyCollection<string> numerosExistentesNoQuadro)
    {
        var problemasDeRegra = regras
            .SelectMany(regra => regra.Value.Validar().Select(problema => $"{CodigosDeTipoDeCarga.Codigo(regra.Key)}: {problema}"))
            .ToList();
        if (problemasDeRegra.Count > 0)
            throw new ArgumentException("Regras de agrupamento inválidas: " + string.Join("; ", problemasDeRegra), nameof(regras));

        var problemasDeNumeracao = numeracao.Validar();
        if (problemasDeNumeracao.Count > 0)
            throw new ArgumentException("Numeração inválida: " + string.Join("; ", problemasDeNumeracao), nameof(numeracao));

        var ignorados = new List<PontoIgnorado>();
        var avisos = new List<string>();
        var validos = new List<PontoDeCarga>();
        foreach (var ponto in pontos.DistinctBy(ponto => ponto.Id).OrderBy(ponto => ponto.Id))
        {
            var motivo = MotivoParaIgnorar(ponto, regras);
            if (motivo is null) validos.Add(ponto);
            else ignorados.Add(new PontoIgnorado(ponto.Id, motivo));
        }

        var circuitos = new List<CircuitoPlanejado>();
        var proximoNumero = new Dictionary<TipoDeCarga, int>();
        var grupos = validos
            .GroupBy(ponto => (Tipo: ponto.Tipo!.Value, ponto.Alimentacao))
            .OrderBy(grupo => grupo.Key.Tipo)
            .ThenBy(grupo => grupo.Key.Alimentacao, StringComparer.Ordinal);

        foreach (var grupo in grupos)
        {
            var tipo = grupo.Key.Tipo;
            var regra = regras.GetValueOrDefault(tipo) ?? SemLimites;
            foreach (var membros in Dividir(grupo.OrderBy(ponto => ponto.Id).ToList(), regra, tipo, avisos))
            {
                if (!proximoNumero.TryGetValue(tipo, out var numero))
                    numero = numeracao.MaiorNumeroExistente(tipo, numerosExistentesNoQuadro) + 1;

                circuitos.Add(new CircuitoPlanejado(
                    numeracao.Formatar(tipo, numero),
                    tipo,
                    grupo.Key.Alimentacao,
                    membros.Select(ponto => ponto.Id).ToList(),
                    membros.All(ponto => ponto.PotenciaVA is not null) ? membros.Sum(ponto => ponto.PotenciaVA!.Value) : null));
                proximoNumero[tipo] = numero + 1;
            }
        }

        return new PlanoDeCircuitos(circuitos, ignorados, avisos);
    }

    private static string? MotivoParaIgnorar(PontoDeCarga ponto, IReadOnlyDictionary<TipoDeCarga, RegraDeAgrupamento> regras)
    {
        if (ponto.Tipo is not { } tipo) return "não classificado (AMP_TipoCarga vazio ou desconhecido)";
        if (tipo == TipoDeCarga.Reserva) return "Reserva é tipo de circuito, não de ponto de carga";
        if (ponto.CircuitoAtual is not null) return $"já pertence ao circuito '{ponto.CircuitoAtual}'";
        if (regras.GetValueOrDefault(tipo)?.MaximaPotenciaVA is not null && ponto.PotenciaVA is null)
            return $"sem potência (AMP_PotenciaInstaladaVA), exigida pelo limite de VA de {CodigosDeTipoDeCarga.Codigo(tipo)}";
        return null;
    }

    private static List<List<PontoDeCarga>> Dividir(List<PontoDeCarga> pontos, RegraDeAgrupamento regra, TipoDeCarga tipo, List<string> avisos)
    {
        var circuitos = new List<List<PontoDeCarga>>();
        var atual = new List<PontoDeCarga>();
        var soma = 0m;
        foreach (var ponto in pontos)
        {
            var potencia = ponto.PotenciaVA ?? 0m;
            var estouraPontos = regra.MaximoDePontos is { } maximo && atual.Count + 1 > maximo;
            var estouraPotencia = regra.MaximaPotenciaVA is { } limite && soma + potencia > limite;
            if (atual.Count > 0 && (regra.CircuitoExclusivo || estouraPontos || estouraPotencia))
            {
                circuitos.Add(atual);
                atual = [];
                soma = 0m;
            }

            if (regra.MaximaPotenciaVA is { } limiteDoTipo && potencia > limiteDoTipo)
            {
                avisos.Add(string.Create(CultureInfo.InvariantCulture,
                    $"ponto {ponto.Id} ({NumeroEmTexto.Formatar(potencia)} VA) excede sozinho o limite de {NumeroEmTexto.Formatar(limiteDoTipo)} VA de {CodigosDeTipoDeCarga.Codigo(tipo)}"));
            }

            atual.Add(ponto);
            soma += potencia;
        }

        if (atual.Count > 0) circuitos.Add(atual);
        return circuitos;
    }
}
