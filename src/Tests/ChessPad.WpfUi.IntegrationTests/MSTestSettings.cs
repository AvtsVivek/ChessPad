// Each case owns a child process and temporary log directory, isolating WPF and logging state.
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
