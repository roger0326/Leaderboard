# Leaderboard

This is an example for custom's score leadboard.
The pressure in the specific production environment is uncertain, so two optimization strategies were mainly implemented: hashing and sharding customer data, and skiplist for ranking data
To continue optimizing, we need to consider using Redis to both skiplist and partition them, which means that the ranking data is also processed in memory by segmenting and sharding it into sections before skipping tables.
However, currently a large amount of data can be processed in memory, and if the data is even larger, it is not suitable to store it all in memory Although the optimization has not continued in this way, the idea has been basically implemented in customer data sharding,
only by changing the hash sharding of customer IDs to point segmentation sharding.
Perhaps there may be complexity in querying regional data across shards, but it is not difficult to achieve through high and low score sharding.
If the ranking query for a certain region is frequent, it is indeed possible to consider optimizing it in this way, such as the top ten rankings, to reduce the pressure on other less visited ranking sharding

## Table of Contents

- [Run](#Run)
- [API](#API)



## Run

Using .NET 8 Minimal API, just run the following command:

```
dotnet run
```

## API

GET		"localhost:port/firstrun" :Initialize 10000 pieces of data.

POST	"localhost:port//customer/{customerId}/score/{score}" Add customer scores or Accumulate scores for existing customer.

GET		"localhost:port/leaderboard/{customerId}?high=0&low=0" :Get the ranking list of the areas near the current customer.

GET		"localhost:port/leaderboard?start=start&end=end" :regional ranking list.
