// Cases own their models and factory delegates, so no shared application state requires serialization.
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
