using Ampere.Core.Parametros;

namespace Ampere.Revit.Parametros;

/// <summary>
///     Tradução entre os tipos e categorias do núcleo e os identificadores nativos do Revit.
/// </summary>
internal static class MapeamentoRevit
{
    private static readonly Dictionary<long, CategoriaEletrica> CategoriasPorId = Enum.GetValues<CategoriaEletrica>()
        .ToDictionary(categoria => (long)Categoria(categoria));

    public static ForgeTypeId Especificacao(TipoDeDadoDoParametro tipo) => tipo switch
    {
        TipoDeDadoDoParametro.Texto => SpecTypeId.String.Text,
        TipoDeDadoDoParametro.Numero => SpecTypeId.Number,
        TipoDeDadoDoParametro.PotenciaAparente => SpecTypeId.ApparentPower,
        TipoDeDadoDoParametro.Corrente => SpecTypeId.Current,
        TipoDeDadoDoParametro.Comprimento => SpecTypeId.Length,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de dado sem especificação Revit.")
    };

    /// <summary>Tipo do núcleo para a especificação do Revit, ou <c>null</c> se não for um tipo usado pelo Ampere.</summary>
    /// <remarks>O operador == de <see cref="ForgeTypeId" /> ignora a versão do identificador (NameEquals).</remarks>
    public static TipoDeDadoDoParametro? Tipo(ForgeTypeId especificacao)
    {
        if (especificacao == SpecTypeId.String.Text) return TipoDeDadoDoParametro.Texto;
        if (especificacao == SpecTypeId.Number) return TipoDeDadoDoParametro.Numero;
        if (especificacao == SpecTypeId.ApparentPower) return TipoDeDadoDoParametro.PotenciaAparente;
        if (especificacao == SpecTypeId.Current) return TipoDeDadoDoParametro.Corrente;
        if (especificacao == SpecTypeId.Length) return TipoDeDadoDoParametro.Comprimento;
        return null;
    }

    public static BuiltInCategory Categoria(CategoriaEletrica categoria) => categoria switch
    {
        CategoriaEletrica.Luminarias => BuiltInCategory.OST_LightingFixtures,
        CategoriaEletrica.DispositivosDeIluminacao => BuiltInCategory.OST_LightingDevices,
        CategoriaEletrica.DispositivosEletricos => BuiltInCategory.OST_ElectricalFixtures,
        CategoriaEletrica.EquipamentosMecanicos => BuiltInCategory.OST_MechanicalEquipment,
        CategoriaEletrica.EquipamentosEletricos => BuiltInCategory.OST_ElectricalEquipment,
        CategoriaEletrica.CircuitosEletricos => BuiltInCategory.OST_ElectricalCircuit,
        _ => throw new ArgumentOutOfRangeException(nameof(categoria), categoria, "Categoria sem equivalente no Revit.")
    };

    /// <summary>Categoria do núcleo para a categoria do Revit, ou <c>null</c> se o Ampere não a usa.</summary>
    public static CategoriaEletrica? CategoriaAmpere(Category categoria) =>
        CategoriasPorId.TryGetValue(categoria.Id.Value, out var ampere) ? ampere : null;
}
