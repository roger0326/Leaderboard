# Leaderboard

This is an example for custom's score leadboard.

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
