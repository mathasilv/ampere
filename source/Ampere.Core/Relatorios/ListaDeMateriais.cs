using System.Text;
using Ampere.Core.Dimensionamento;

namespace Ampere.Core.Relatorios;

/// <summary>Um item da lista de materiais e os circuitos de onde ele vem.</summary>
/// <param name="Grupo">Condutores, Disjuntores ou IDR.</param>
/// <param name="Observacao">O que o item não define (ex.: curva do disjuntor) ou a simplificação usada.</param>
public sealed record ItemDeMaterial(string Grupo, string Item, decimal Quantidade, string Unidade, IReadOnlyList<string> Circuitos, string Observacao);

/// <summary>Circuito que ficou fora da lista, e por quê.</summary>
public sealed record CircuitoForaDaLista(string Circuito, string Motivo);

/// <summary>
///     Lista de materiais dos circuitos dimensionados: condutores (metros por tipo, seção e função), disjuntores e IDR
///     (unidades por polos e correntes). Sai da mesma rodada das memórias, então confere com elas.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Entra o circuito com a proteção decidida por completo (o mesmo critério da gravação no modelo); os outros
///         vão para <see cref="ForaDaLista" /> com o motivo.</item>
///         <item>Condutores: comprimento do circuito × condutores (fases, neutro e proteção), sem sobras nem emendas, com as
///         seções da memória: neutro com a da fase, condutor de proteção pela tabela do perfil. Circuito sem a seção do
///         condutor de proteção fica fora da lista.</item>
///         <item>Disjuntor com um polo por fase, e a capacidade de interrupção mínima quando as condições trazem a corrente
///         de curto-circuito presumida; IDR com um polo por condutor vivo (fases e neutro). Curva e tipo do IDR não são
///         decididos pelo Ampere.</item>
///         <item>Eletrodutos ficam de fora: vários circuitos dividem o mesmo trecho, e somar comprimentos de circuito daria
///         quantidade falsa. O quantitativo deles é o dos eletrodutos modelados no Revit.</item>
///     </list>
/// </remarks>
public sealed record ListaDeMateriais(IReadOnlyList<ItemDeMaterial> Itens, IReadOnlyList<CircuitoForaDaLista> ForaDaLista)
{
    public const string Condutores = "Condutores";
    public const string Disjuntores = "Disjuntores";
    public const string Idr = "IDR";

    private static readonly string[] Cabecalho = ["Grupo", "Item", "Quantidade", "Unidade", "Circuitos", "Observação"];

    private static readonly string[] Funcoes = ["fase", "neutro", "proteção (PE)"];

    public static ListaDeMateriais Montar(IReadOnlyList<ResultadoDoCircuito> resultados)
    {
        var condutores = new Dictionary<(string Tipo, string Isolamento, decimal Secao, int Funcao), Acumulado>();
        var disjuntores = new Dictionary<(int Polos, decimal Corrente, decimal? Interrupcao), Acumulado>();
        var idrs = new Dictionary<(int Polos, decimal Corrente, decimal Sensibilidade), Acumulado>();
        var fora = new List<CircuitoForaDaLista>();

        foreach (var resultado in resultados)
        {
            var circuito = Identificacao(resultado);
            if (Motivo(resultado) is { } motivo)
            {
                fora.Add(new CircuitoForaDaLista(circuito, motivo));
                continue;
            }

            var entrada = resultado.Entrada!;
            var calculo = resultado.Dimensionamento!;
            if (Composicao(entrada.Fases) is not { } composicao)
            {
                fora.Add(new CircuitoForaDaLista(circuito, $"configuração {entrada.Fases} sem composição de condutores"));
                continue;
            }

            var (fases, neutro, polosDoIdr) = composicao;

            var tipo = string.IsNullOrWhiteSpace(entrada.TipoDeCondutor) ? "tipo não informado" : entrada.TipoDeCondutor.Trim();
            var isolamento = $"{entrada.Material.Trim()}, {entrada.Isolacao.Trim()}";
            var secao = calculo.SecaoMm2!.Value;
            Somar(condutores, (tipo, isolamento, secao, 0), entrada.ComprimentoM * fases, circuito);
            if (neutro) Somar(condutores, (tipo, isolamento, calculo.SecaoDoNeutroMm2 ?? secao, 1), entrada.ComprimentoM, circuito);
            Somar(condutores, (tipo, isolamento, calculo.SecaoDeProtecaoMm2!.Value, 2), entrada.ComprimentoM, circuito);
            Somar(disjuntores, (fases, calculo.DisjuntorA!.Value, calculo.CapacidadeDeInterrupcaoKa), 1m, circuito);
            if (calculo.IdrNominalA is { } nominal && calculo.IdrSensibilidadeMa is { } sensibilidade)
                Somar(idrs, (polosDoIdr, nominal, sensibilidade), 1m, circuito);
        }

        var itens = new List<ItemDeMaterial>();
        itens.AddRange(condutores
            .OrderBy(par => par.Key.Tipo, StringComparer.Ordinal).ThenBy(par => par.Key.Isolamento, StringComparer.Ordinal)
            .ThenBy(par => par.Key.Secao).ThenBy(par => par.Key.Funcao)
            .Select(par => new ItemDeMaterial(Condutores, $"{par.Key.Tipo} ({par.Key.Isolamento}) {Numero(par.Key.Secao)} mm² — {Funcoes[par.Key.Funcao]}",
                par.Value.Quantidade, "m", par.Value.Circuitos,
                par.Key.Funcao switch
                {
                    0 => "comprimento do circuito × fases, sem sobras nem emendas",
                    1 => "comprimento do circuito, sem sobras nem emendas; seção da fase (sem a redução que a norma permite)",
                    _ => "comprimento do circuito, sem sobras nem emendas; seção pela tabela do condutor de proteção"
                })));
        itens.AddRange(disjuntores
            .OrderBy(par => par.Key.Polos).ThenBy(par => par.Key.Corrente).ThenBy(par => par.Key.Interrupcao ?? 0m)
            .Select(par => new ItemDeMaterial(Disjuntores,
                $"Disjuntor {par.Key.Polos}P {Numero(par.Key.Corrente)} A" + (par.Key.Interrupcao is { } capacidade ? $", capacidade de interrupção ≥ {Numero(capacidade)} kA" : string.Empty),
                par.Value.Quantidade, "un", par.Value.Circuitos,
                par.Key.Interrupcao is null
                    ? "curva e capacidade de interrupção a definir (sem a corrente de curto-circuito presumida nas condições)"
                    : "curva a definir; I²t que deixa passar no máximo o k²S² de cada circuito (memória)")));
        itens.AddRange(idrs
            .OrderBy(par => par.Key.Polos).ThenBy(par => par.Key.Corrente).ThenBy(par => par.Key.Sensibilidade)
            .Select(par => new ItemDeMaterial(Idr, $"IDR {par.Key.Polos}P {Numero(par.Key.Corrente)} A, IΔn {Numero(par.Key.Sensibilidade)} mA",
                par.Value.Quantidade, "un", par.Value.Circuitos, "tipo (AC, A…) a definir")));
        return new ListaDeMateriais(itens, fora);
    }

    /// <summary>Planilha para o Excel em português; os circuitos fora da lista vêm no fim, com o motivo.</summary>
    public string Csv()
    {
        var texto = new StringBuilder();
        CsvEmPortugues.Linha(texto, Cabecalho);
        foreach (var item in Itens)
            CsvEmPortugues.Linha(texto, [item.Grupo, item.Item, Numero(item.Quantidade), item.Unidade, string.Join(" | ", item.Circuitos), item.Observacao]);
        foreach (var circuito in ForaDaLista)
            CsvEmPortugues.Linha(texto, ["Fora da lista", circuito.Circuito, null, null, null, circuito.Motivo]);
        return texto.ToString();
    }

    private static string? Motivo(ResultadoDoCircuito resultado)
    {
        if (resultado.ProblemasDeDados.Count > 0) return $"dados faltando: {string.Join(" | ", resultado.ProblemasDeDados)}";
        if (resultado.Dimensionamento is { Situacao: SituacaoDoDimensionamento.EntradaInvalida } invalido)
            return $"entrada inválida: {string.Join(" | ", invalido.Problemas)}";
        if (resultado.Dimensionamento is not { } calculo || resultado.Entrada is null || resultado.Memoria is null) return "não calculado";
        if (!calculo.IdrAvaliado || calculo.SecaoMm2 is null || calculo.DisjuntorA is null)
            return $"proteção não decidida: {string.Join(" | ", calculo.Problemas)}";
        if (calculo.SecaoDeProtecaoMm2 is null) return $"seção do condutor de proteção não calculada: {string.Join(" | ", calculo.Problemas)}";
        return null;
    }

    // Fases, se há neutro e os polos do IDR (condutores vivos).
    private static (int Fases, bool Neutro, int PolosDoIdr)? Composicao(string fases) => fases switch
    {
        "F+N" => (1, true, 2),
        "2F" => (2, false, 2),
        "3F" => (3, false, 3),
        "3F+N" => (3, true, 4),
        _ => null
    };

    private static string Identificacao(ResultadoDoCircuito resultado)
    {
        var numero = string.IsNullOrWhiteSpace(resultado.Numero) ? $"circuito {resultado.Id}" : resultado.Numero;
        return string.IsNullOrWhiteSpace(resultado.Quadro) ? numero : $"{resultado.Quadro}-{numero}";
    }

    private static void Somar<TChave>(Dictionary<TChave, Acumulado> itens, TChave chave, decimal quantidade, string circuito) where TChave : notnull
    {
        if (!itens.TryGetValue(chave, out var acumulado)) itens[chave] = acumulado = new Acumulado();
        acumulado.Quantidade += quantidade;
        if (!acumulado.Circuitos.Contains(circuito, StringComparer.Ordinal)) acumulado.Circuitos.Add(circuito);
    }

    private static string Numero(decimal valor) => NumeroEmTexto.FormatarParaLeitura(valor);

    private sealed class Acumulado
    {
        public decimal Quantidade { get; set; }

        public List<string> Circuitos { get; } = [];
    }
}
