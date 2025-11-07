# SystemProgramming
**SysProgPRVI** - lightweight .NET HTTP service that exposes a /city endpoint and returns air-quality data (proxied from the IQAir API). Includes in-memory LFU caching, console logging, and an optional HTML client for quick testing.

**SysProgDRUGI** - asynchronous version of the air-quality service built with async/await. Requests are handled concurrently, remote API calls are non-blocking, and the existing in-memory cache is preserved. Suitable for scenarios with multiple simultaneous clients.

**SysProgTRECII** - reactive .NET application for basic sentiment analysis over incoming text. The app periodically pulls content from the NYT Most Popular API (e.g. most-viewed articles), runs each title/abstract through a simple sentiment classifier (positive / negative / neutral), and emits the results to subscribers using the reactive pattern (IObservable / IObserver). It’s designed to demonstrate a push-based data flow, separation of concerns, and real-time reaction to new events. 
(Note: requires an NYT API key provided via configuration.)
