namespace CachedInventory;

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public static class CachedInventoryApiBuilder
{
  public static WebApplication Build(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddScoped<IWarehouseStockSystemClient, WarehouseStockSystemClient>();

    var cache = new ConcurrentDictionary<int, int>();
    var timers = new ConcurrentDictionary<int, Timer>();

    builder.Services.AddSingleton(timers);
    builder.Services.AddSingleton(cache);

    var app = builder.Build();

    var debounceDispatcher = new DebounceDispatcher(TimeSpan.FromMilliseconds(150));

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
      app.UseSwagger();
      app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.MapGet(
      "/stock/{productId:int}",
      async (
        [FromServices] IWarehouseStockSystemClient client,
        [FromServices] ConcurrentDictionary<int, int> cache,
        int productId
      ) =>
      {
        if (cache.TryGetValue(productId, out var cachedStock))
        {
          return Results.Ok(cachedStock);
        }

        var stock = await client.GetStock(productId);
        cache[productId] = stock;
        return Results.Ok(stock);
      }
    )
      .WithName("GetStock")
      .WithOpenApi();

    app.MapPost(
      "/stock/retrieve",
      async (
        [FromServices] IWarehouseStockSystemClient client,
        [FromServices] ConcurrentDictionary<int, int> cache,
        [FromServices] ConcurrentDictionary<int, Timer> timers,
        [FromBody] RetrieveStockRequest req
      ) =>
      {
        // var stock = await client.GetStock(req.ProductId);
        // if (stock < req.Amount)
        // {
        //   return Results.BadRequest("Not enough stock.");
        // }

        // await client.UpdateStock(req.ProductId, stock - req.Amount);
        // return Results.Ok();
        Debug.WriteLine("\nRetrieveEP");
        Debug.WriteLine($"{req}");

        var productId = req.ProductId;
        var stock = await GetStockFromCacheOrOldSystem(client, cache, productId);

        // if (stock < req.Amount)
        // {
        //   Debug.WriteLine("\tNot enough stock");
        //   return Results.BadRequest("Not enough stock.");
        // }

        Debug.WriteLine("\tUpdating cache");

        cache.TryGetValue(productId, out var stk);
        Debug.WriteLine($"\t\tFrom: {stk} pc");

        var newQty = stk - req.Amount;
        cache.AddOrUpdate(
          productId,
          newQty,
          (k, oldValue) => {
            // client.UpdateStock(productId, oldValue - req.Amount);
            return oldValue - req.Amount;
          }
        );

        cache.TryGetValue(productId, out var testito);
        Debug.WriteLine($"\t\tTo: {testito} pc");


        Debug.WriteLine("\tUpdating old API");
        // var updtTask = client.UpdateStock(productId, testito); //2.5s
        // debounceDispatcher.Debounce(
        //   async () => await client.UpdateStock(productId, testito)
        // );
        ResetTimer(req.ProductId, client, cache, timers);

        return Results.Ok();
      }
    )
      .WithName("RetrieveStock")
      .WithOpenApi();

    app.MapPost(
      "/stock/restock",
      async (
        [FromServices] IWarehouseStockSystemClient client, [FromBody] RestockRequest req
      ) =>
      {
        // var stock = await client.GetStock(req.ProductId);
        // await client.UpdateStock(req.ProductId, req.Amount + stock);
        // return Results.Ok();
        Debug.WriteLine("\nRestockEP");
        Debug.WriteLine($"{req}");

        var productId = req.ProductId;
        var stock = await GetStockFromCacheOrOldSystem(client, cache, productId);

        Debug.WriteLine("\tUpdating cache");
        Debug.WriteLine($"\t\tFrom: {stock} pc");
        
        cache.TryGetValue(productId, out var stk);
        var newQty = req.Amount + stk;
        cache.TryUpdate(productId, newQty, stock);

        Debug.WriteLine($"\t\tTo: {cache[productId]} pc");

        Debug.WriteLine("\tUpdating old system");
        Debug.WriteLine($"\t\tFrom: {cache[productId]} pc");
        await client.UpdateStock(productId, stk + req.Amount); //2.5s
        
        return Results.Ok();
      }
    )
      .WithName("Restock")
      .WithOpenApi();

    return app;

    static async Task<int> GetStockFromCacheOrOldSystem
    (
    [FromServices] IWarehouseStockSystemClient oldSystem,
    [FromServices] ConcurrentDictionary<int, int> cache,
      int itemId
    )
    {
      if (!cache.TryGetValue(itemId, out var stock))
      {
        Debug.WriteLine("\tItem not in cache");
        Debug.WriteLine("\t\tQuerying old API");
        var oldSystemStock = await oldSystem.GetStock(itemId);
        
        Debug.WriteLine($"\t\tOld system returned {oldSystemStock}");
        Debug.WriteLine("\t\tAdding old system response to cache");
        cache.TryAdd(itemId, oldSystemStock);

        cache.TryGetValue(itemId, out var ans);
        Debug.WriteLine($"\tReturning stock of: {ans}");
        return ans;
      }

        Debug.WriteLine("\tItem found in cache");
        cache.TryGetValue(itemId, out var ans2);
        Debug.WriteLine($"\tReturning stock of: {ans2}");
        return ans2;
    }

    
  }
  private static void ResetTimer(
    int productId,
    IWarehouseStockSystemClient client,
    ConcurrentDictionary<int, int> cache,
    ConcurrentDictionary<int, Timer> timers
  )
  {
    if (timers.TryGetValue(productId, out var existingTimer))
    {
      existingTimer.Change(500, Timeout.Infinite);
    }
    else
    {
      var newTimer = new Timer(
        async state =>
        {
          if (state != null)
          {
            var pid = (int)state;
            if (cache.TryGetValue(pid, out var stock))
            {
              await client.UpdateStock(pid, stock);
            }
          }
        },
        productId,
        500,
        Timeout.Infinite
      );
      timers[productId] = newTimer;
    }
  }
}

public record RetrieveStockRequest(int ProductId, int Amount);

public record RestockRequest(int ProductId, int Amount);

