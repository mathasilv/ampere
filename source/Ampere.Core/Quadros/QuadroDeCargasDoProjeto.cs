using Ampere.Core.Cargas;
using Ampere.Core.Normas;

namespace Ampere.Core.Quadros;

/// <summary>Circuito lido do documento, ainda sem validação.</summary>
/// <param name="Numero">AMP_NumeroCircuito do sistema (ou o nome nativo).</param>
/// <param name="Tipo">AMP_TipoCarga do sistema (código), se gravado.</param>
/// <param name="PotenciaVA">Soma de AMP_PotenciaInstaladaVA dos membros, se houver.</param>
/// <param name="Fases">AMP_Fases do primeiro membro que tiver (ex.: "F+N").</param>
/// <param name="TensaoV">AMP_TensaoCircuitoV do primeiro membro que tiver.</param>
public sealed record CircuitoLido(string? Numero, string? Tipo, decimal? PotenciaVA, string? Fases, decimal? TensaoV);

/// <summary>Quadro do documento com os seus circuitos lidos.</summary>
public sealed record QuadroLido(long Id, string Nome, IReadOnlyList<CircuitoLido> Circuitos);

/// <summary>Resultado do quadro de cargas de um quadro do documento.</summary>
public sealed record ResultadoDoQuadro(long Id, string Nome, ResultadoDoQuadroDeCargas Quadro);

/// <summary>
///     Porta para os quadros e circuitos de um documento no caso de uso do quadro de cargas (implementada pelo
///     adapter Revit). Somente leitura — montar quadro não altera o documento.
/// </summary>
public interface IDocumentoDeQuadros
{
    /// <summary>Quadros com circuitos atribuídos; quadro sem circuito fica de fora.</summary>
    IReadOnlyList<QuadroLido> LerQuadrosComCircuitos();
}

/// <summary>
///     Caso de uso "Montar quadros de cargas" (especificação §3, F1.4): lê os quadros, valida os circuitos e monta
///     cada quadro contra o perfil. Não grava nada — o resultado é para exibir e para os relatórios.
/// </summary>
/// <remarks>
///     Esquema e tensão do quadro vêm dos próprios circuitos (AMP_Fases e AMP_TensaoCircuitoV uniformes): mistos ou
///     ausentes deixam a corrente sem cálculo, com o motivo nos problemas — o motor não escolhe por conta própria.
/// </remarks>
public static class QuadroDeCargasDoProjeto
{
    public static IReadOnlyList<ResultadoDoQuadro> Executar(
        IDocumentoDeQuadros documento,
        PerfilNormativo perfil,
        IReadOnlyDictionary<TipoDeCarga, decimal>? fatoresInformados = null)
    {
        var resultados = new List<ResultadoDoQuadro>();
        foreach (var quadro in documento.LerQuadrosComCircuitos())
        {
            var problemas = new List<string>();
            var circuitos = new List<CircuitoDoQuadro>();
            foreach (var lido in quadro.Circuitos)
            {
                var numero = string.IsNullOrWhiteSpace(lido.Numero) ? "(sem número)" : lido.Numero.Trim();
                if (!CodigosDeTipoDeCarga.TryLer(lido.Tipo, out var tipo))
                {
                    problemas.Add($"circuito {numero}: sem AMP_TipoCarga reconhecido (crie os circuitos com o Ampere ou classifique-os)");
                    continue;
                }

                if (lido.PotenciaVA is not { } potencia)
                {
                    problemas.Add($"circuito {numero}: sem potência instalada (classifique os pontos com AMP_PotenciaInstaladaVA)");
                    continue;
                }

                circuitos.Add(new CircuitoDoQuadro(numero, null, tipo, potencia));
            }

            // O par (esquema, tensão) do quadro precisa ser único entre os circuitos que o informam.
            var pares = quadro.Circuitos
                .Where(lido => lido.Fases is { Length: > 0 } && lido.TensaoV is > 0)
                .Select(lido => (lido.Fases!.Trim(), lido.TensaoV!.Value))
                .Distinct()
                .ToList();
            string esquema;
            decimal tensao;
            switch (pares.Count)
            {
                case 1:
                    (esquema, tensao) = pares[0];
                    break;
                case 0:
                    esquema = "F+N";
                    tensao = 0m;
                    problemas.Add("nenhum circuito com AMP_Fases e AMP_TensaoCircuitoV: corrente do quadro não calculada");
                    break;
                default:
                    esquema = "F+N";
                    tensao = 0m;
                    problemas.Add($"circuitos com esquemas/tensões diferentes ({string.Join(", ", pares.Select(par => $"{par.Item1} {NumeroEmTexto.Formatar(par.Item2)} V"))}): corrente do quadro não calculada");
                    break;
            }

            var montado = QuadroDeCargas.Montar(quadro.Nome, esquema, tensao, circuitos, perfil, fatoresInformados);
            if (problemas.Count > 0) montado = montado with { Problemas = [.. montado.Problemas, .. problemas] };
            resultados.Add(new ResultadoDoQuadro(quadro.Id, quadro.Nome, montado));
        }

        return resultados;
    }
}
