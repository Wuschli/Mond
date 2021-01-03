using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mond.Debugger;
using Mond.Libraries;
using Mond.VirtualMachine;
using Mond.VirtualMachine.Persistence;

[assembly: InternalsVisibleTo("Mond.Tests")]

namespace Mond
{
    public delegate MondValue MondFunction(MondState state, params MondValue[] arguments);

    public delegate MondValue MondInstanceFunction(MondState state, MondValue instance, params MondValue[] arguments);

    public class MondState
    {
        private readonly Machine _machine;
        private readonly Dictionary<string, MondValue> _prototypeCache;
        private MondLibraryManager _libraries;
        private bool _librariesLoaded;

        public MondState()
        {
            _machine = new Machine(this);
            _prototypeCache = new Dictionary<string, MondValue>();
            _librariesLoaded = false;

            Options = new MondCompilerOptions();

            RuntimeOptions = new MondRuntimeOptions();

            Libraries = new MondLibraryManager
            {
                new StandardLibraries()
            };
        }

        /// <summary>
        /// Gets or sets the options to use when compiling scripts with <c>Run</c>.
        /// </summary>
        public MondCompilerOptions Options { get; set; }

        /// <summary>
        /// Gets or sets the options to use when running programs.
        /// </summary>
        public MondRuntimeOptions RuntimeOptions { get; set; }

        /// <summary>
        /// Gets or sets the libraries to load into the state.
        /// </summary>
        public MondLibraryManager Libraries
        {
            get => _libraries;
            set
            {
                if (_librariesLoaded)
                    throw new InvalidOperationException(MondLibraryManager.LockedError);

                _libraries = value;
            }
        }

        /// <summary>
        /// Gets or sets the debugger that is currently attached to the state.
        /// </summary>
        public MondDebugger Debugger
        {
            get => _machine.Debugger;
            set
            {
                _machine.Debugger?.Detach();

                if (value != null && !value.TryAttach())
                    throw new InvalidOperationException("Debuggers cannot be attached to more than one state at a time");

                _machine.Debugger = value;
            }
        }

        /// <summary>
        /// Gets or sets global values in the state.
        /// </summary>
        public MondValue this[MondValue index]
        {
            get => _machine.Global[index];
            set => _machine.Global[index] = value;
        }

        /// <summary>
        /// Gets the global object that holds global values for the state.
        /// </summary>
        public MondValue Global => _machine.Global;

        /// <summary>
        /// This flag will be set when the vm exited because it ran out of gas.
        /// </summary>
        public bool GasLimitExceeded => _machine.GasLimitExceeded;

        /// <summary>
        /// Compiles and runs a Mond script from source code.
        /// </summary>
        public MondValue Run(string sourceCode, string fileName = null)
        {
            EnsureLibrariesLoaded();

            if (Libraries != null)
            {
                Options.FirstLineNumber = 0;
                sourceCode = Libraries.Definitions + sourceCode;
            }

            var program = MondProgram.Compile(sourceCode, fileName, Options);

            return Load(program);
        }

        /// <summary>
        /// Runs a precompiled Mond script.
        /// </summary>
        public MondValue Load(MondProgram program)
        {
            EnsureLibrariesLoaded();

            return _machine.Load(program, RuntimeOptions);
        }

        /// <summary>
        /// Calls a Mond function.
        /// </summary>
        public MondValue Call(MondValue function, params MondValue[] arguments)
        {
            return _machine.Call(function, RuntimeOptions, arguments);
        }

        /// <summary>
        /// Continue execution of the last run.
        /// </summary>
        /// <returns></returns>
        public MondValue Continue()
        {
            return _machine.Run(RuntimeOptions, false);
        }

        /// <summary>
        /// Loads the libraries if they weren't already loaded.
        /// </summary>
        public void EnsureLibrariesLoaded()
        {
            if (_librariesLoaded)
                return;

            if (Libraries == null)
            {
                _librariesLoaded = true;
                return;
            }

            Libraries.Load(this, libs =>
            {
                var requireLib = libs.Get<RequireLibrary>();

                if (requireLib != null)
                    requireLib.Options = Options;
            });

            _librariesLoaded = true;
        }

        /// <summary>
        /// Gets the file name of the currently running script.
        /// </summary>
        public string CurrentScript => _machine.CurrentScript;

        /// <summary>
        /// Finds the generated prototype for a bound class or module.
        /// </summary>
        public MondValue FindPrototype(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            _prototypeCache.TryGetValue(name, out var value);
            return value;
        }

        public void Deserialize(IStateSerializer stateSerializer, byte[] serialized)
        {
            var machineState = stateSerializer.Deserialize(serialized);
            _machine.MachineState = machineState;
        }

        public byte[] Serialize(IStateSerializer stateSerializer)
        {
            return stateSerializer.Serialize(_machine.MachineState);
        }

        internal bool TryAddPrototype(string name, MondValue value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            if (value == null || value.Type != MondValueType.Object)
                throw new ArgumentException("Prototype value must be an object.", nameof(value));

            if (_prototypeCache.ContainsKey(name))
                return false;

            _prototypeCache.Add(name, value);
            return true;
        }
    }
}