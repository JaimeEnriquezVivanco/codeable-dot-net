// ReSharper disable ClassNeverInstantiated.Global

namespace CachedInventory.Tests;

// public class SingleRetrieval
// {
//   [Fact(DisplayName = "retirar un producto")]
//   public static async Task Test() => await TestApiPerformance.Test(1, [3], false, 2_000);
// }

// public class FourRetrievalsInParallel
// {
//   [Fact(DisplayName = "retirar cuatro productos en paralelo")]
//   public static async Task Test() => await TestApiPerformance.Test(2, [1, 2, 3, 4], true, 1_000);
// }

// public class FourRetrievalsSequentially
// {
//   [Fact(DisplayName = "retirar cuatro productos secuencialmente")]
//   public static async Task Test() => await TestApiPerformance.Test(3, [1, 2, 3, 4], false, 1_000);
// }

// public class SevenRetrievalsInParallel
// {
//   [Fact(DisplayName = "retirar siete productos en paralelo")]
//   public static async Task Test() => await TestApiPerformance.Test(4, [1, 2, 3, 4, 5, 6, 7], true, 500);
// }

// public class SevenRetrievalsSequentially
// {
//   [Fact(DisplayName = "retirar siete productos secuencialmente")]
//   public static async Task Test() => await TestApiPerformance.Test(59, [1, 2, 3, 4, 5, 6, 7], false, 500);
// }

// internal static class TestApiPerformance
// {
//   internal static async Task Test(int productId, int[] retrievals, bool isParallel, long expectedPerformance)
//   {
//     await using var setup = await TestSetup.Initialize();
//     await setup.Restock(productId, retrievals.Sum());
//     await setup.VerifyStockFromFile(productId, retrievals.Sum());
//     var tasks = new List<Task>();
//     foreach (var retrieval in retrievals)
//     {
//       var task = setup.Retrieve(productId, retrieval);
//       if (!isParallel)
//       {
//         await task;
//       }

//       tasks.Add(task);
//     }

//     await Task.WhenAll(tasks);
//     var finalStock = await setup.GetStock(productId);
//     Assert.True(finalStock == 0, $"El stock final no es 0, sino {finalStock}.");
//     Assert.True(
//       setup.AverageRequestDuration < expectedPerformance,
//       $"Duración promedio: {setup.AverageRequestDuration}ms, se esperaba un máximo de {expectedPerformance}ms.");
//     await setup.VerifyStockFromFile(productId, 0);
//   }
// }

public class ExcessiveRetrievalsMakeStockNegative
{
  [Fact(DisplayName = "Retiros excesivos no deben ser procesados")]
  public static async Task Test() => await ExtraTest.Test(19, 1, [2], false, 1_000);
}

internal static class ExtraTest
{
  internal static async Task Test(
    int productId,
    int initialStock,
    int[] retrievals,
    bool isParallel,
    long expectedPerformance)
  {
    await using var setup = await TestSetup.Initialize();
    
    await setup.Restock(productId, initialStock);
    await setup.VerifyStockFromFile(productId, initialStock);
    
    var tasks = new List<Task>();
    foreach (var retrieval in retrievals)
    {
      var task = setup.Retrieve(productId, retrieval);
      if (!isParallel)
      {
        await task;
      }
      tasks.Add(task);
    }
    await Task.WhenAll(tasks);
    
    var finalStock = await setup.GetStock(productId);

    Assert.True(finalStock >= 0 , $"El stock no puede ser negativo. El stock final es {finalStock}.");
    
    Assert.True(
      setup.AverageRequestDuration < expectedPerformance,
      $"Duración promedio: {setup.AverageRequestDuration}ms, se esperaba un máximo de {expectedPerformance}ms.");

    // No se hace la última verificación
    // await setup.VerifyStockFromFile(productId, 0);
  }
}
