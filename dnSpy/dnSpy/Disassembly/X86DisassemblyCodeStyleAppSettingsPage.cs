﻿/*
    Copyright (C) 2014-2018 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using dnSpy.Contracts.Disassembly;
using dnSpy.Contracts.MVVM;
using dnSpy.Contracts.Settings.Dialog;
using Iced.Intel;

namespace dnSpy.Disassembly {
	abstract class X86DisassemblyCodeStyleAppSettingsPage : AppSettingsPage {
		const ulong X86_RIP = 0x7FFF_FFFF_FFFF_FFF0;
		const ulong SYMBOLADDR = 0x5AA556789ABCDEF0UL;
		const string SYMBOLNAME = "secret_data";

		protected readonly X86DisassemblySettings _global_x86DisassemblySettings;
		protected readonly X86DisassemblySettings x86DisassemblySettings;
		readonly StringBuilderFormatterOutput x86Output;
		readonly Formatter formatter;
		readonly List<X86DisasmBooleanSetting> boolSettings;

		public IX86DisassemblySettings Settings => x86DisassemblySettings;
		public sealed override Guid ParentGuid => new Guid(AppSettingsConstants.GUID_DISASSEMBLER_CODESTYLE);
		public sealed override object UIObject => this;

		public X86DisasmBooleanSetting UseHexNumbers { get; }
		public X86DisasmBooleanSetting UpperCasePrefixes { get; }
		public X86DisasmBooleanSetting UpperCaseMnemonics { get; }
		public X86DisasmBooleanSetting UpperCaseRegisters { get; }
		public X86DisasmBooleanSetting UpperCaseKeywords { get; }
		public X86DisasmBooleanSetting UpperCaseHex { get; }
		public X86DisasmBooleanSetting UpperCaseAll { get; }
		public X86DisasmBooleanSetting SpaceAfterOperandSeparator { get; }
		public X86DisasmBooleanSetting SpaceAfterMemoryBracket { get; }
		public X86DisasmBooleanSetting SpaceBetweenMemoryAddOperators { get; }
		public X86DisasmBooleanSetting SpaceBetweenMemoryMulOperators { get; }
		public X86DisasmBooleanSetting ScaleBeforeIndex { get; }
		public X86DisasmBooleanSetting AlwaysShowScale { get; }
		public X86DisasmBooleanSetting AlwaysShowSegmentRegister { get; }
		public X86DisasmBooleanSetting ShowZeroDisplacements { get; }
		public X86DisasmBooleanSetting ShortNumbers { get; }
		public X86DisasmBooleanSetting ShortBranchNumbers { get; }
		public X86DisasmBooleanSetting SmallHexNumbersInDecimal { get; }
		public X86DisasmBooleanSetting AddLeadingZeroToHexNumbers { get; }
		public X86DisasmBooleanSetting SignedImmediateOperands { get; }
		public X86DisasmBooleanSetting SignedMemoryDisplacements { get; }
		public X86DisasmBooleanSetting AlwaysShowMemorySize { get; }
		public X86DisasmBooleanSetting RipRelativeAddresses { get; }
		public X86DisasmBooleanSetting ShowBranchSize { get; }
		public X86DisasmBooleanSetting UsePseudoOps { get; }
		public X86DisasmBooleanSetting ShowSymbolAddress { get; }

		public Int32VM OperandColumnVM { get; }

		public string HexPrefix {
			get => x86DisassemblySettings.HexPrefix ?? string.Empty;
			set {
				if (value != x86DisassemblySettings.HexPrefix) {
					x86DisassemblySettings.HexPrefix = value;
					OnPropertyChanged(nameof(HexPrefix));
					RefreshDisassembly();
				}
			}
		}

		public string HexSuffix {
			get => x86DisassemblySettings.HexSuffix ?? string.Empty;
			set {
				if (value != x86DisassemblySettings.HexSuffix) {
					x86DisassemblySettings.HexSuffix = value;
					OnPropertyChanged(nameof(HexSuffix));
					RefreshDisassembly();
				}
			}
		}

		public string DigitSeparator {
			get => x86DisassemblySettings.DigitSeparator ?? string.Empty;
			set {
				if (value != x86DisassemblySettings.DigitSeparator) {
					x86DisassemblySettings.DigitSeparator = value;
					OnPropertyChanged(nameof(DigitSeparator));
					RefreshDisassembly();
				}
			}
		}

		protected sealed class SymbolResolver : Iced.Intel.ISymbolResolver {
			public static readonly Iced.Intel.ISymbolResolver Instance = new SymbolResolver();
			SymbolResolver() { }

			public bool TryGetSymbol(int operand, int instructionOperand, ref Instruction instruction, ulong address, int addressSize, out SymbolResult symbol) {
				if (address == SYMBOLADDR) {
					symbol = new SymbolResult(SYMBOLADDR, new TextInfo(SYMBOLNAME, FormatterOutputTextKind.Data), SymbolFlags.None);
					return true;
				}
				symbol = default;
				return false;
			}
		}

		protected X86DisassemblyCodeStyleAppSettingsPage(X86DisassemblySettings global_x86DisassemblySettings, X86DisassemblySettings x86DisassemblySettings, Formatter formatter) {
			_global_x86DisassemblySettings = global_x86DisassemblySettings ?? throw new ArgumentNullException(nameof(global_x86DisassemblySettings));
			this.x86DisassemblySettings = x86DisassemblySettings ?? throw new ArgumentNullException(nameof(x86DisassemblySettings));
			x86Output = new StringBuilderFormatterOutput();
			this.formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
			boolSettings = new List<X86DisasmBooleanSetting>();

			UseHexNumbers = AddDisasmBoolSetting(
				() => Settings.NumberBase == Contracts.Disassembly.NumberBase.Hexadecimal,
				value => Settings.NumberBase = value ? Contracts.Disassembly.NumberBase.Hexadecimal : Contracts.Disassembly.NumberBase.Decimal,
				Instruction.Create(Code.Mov_r64_imm64, Register.RDX, 0x123456789ABCDEF0));
			UpperCasePrefixes = AddDisasmBoolSetting(() => Settings.UpperCasePrefixes, value => Settings.UpperCasePrefixes = value, Instruction.CreateMovsb(64, repPrefix: RepPrefixKind.Rep));
			UpperCaseMnemonics = AddDisasmBoolSetting(() => Settings.UpperCaseMnemonics, value => Settings.UpperCaseMnemonics = value, Instruction.Create(Code.Xchg_r64_RAX, Register.RSI, Register.RAX));
			UpperCaseRegisters = AddDisasmBoolSetting(() => Settings.UpperCaseRegisters, value => Settings.UpperCaseRegisters = value, Instruction.Create(Code.Xchg_r64_RAX, Register.RSI, Register.RAX));
			UpperCaseKeywords = AddDisasmBoolSetting(() => Settings.UpperCaseKeywords, value => Settings.UpperCaseKeywords = value, Instruction.Create(Code.Mov_rm8_imm8, new MemoryOperand(Register.RCX, 4, 1), 0x5A));
			UpperCaseHex = AddDisasmBoolSetting(() => Settings.UpperCaseHex, value => Settings.UpperCaseHex = value, Instruction.Create(Code.Mov_r64_imm64, Register.RDX, 0x123456789ABCDEF0));
			UpperCaseAll = AddDisasmBoolSetting(() => Settings.UpperCaseAll, value => Settings.UpperCaseAll = value, Instruction.CreateMovsb(64, repPrefix: RepPrefixKind.Rep));
			SpaceAfterOperandSeparator = AddDisasmBoolSetting(() => Settings.SpaceAfterOperandSeparator, value => Settings.SpaceAfterOperandSeparator = value, Instruction.Create(Code.Shld_rm16_r16_CL, Register.DX, Register.AX, Register.CL));
			SpaceAfterMemoryBracket = AddDisasmBoolSetting(() => Settings.SpaceAfterMemoryBracket, value => Settings.SpaceAfterMemoryBracket = value, Instruction.Create(Code.Push_rm64, new MemoryOperand(Register.RBP, Register.RDI, 4, -0x12345678, 8, false, Register.None)));
			SpaceBetweenMemoryAddOperators = AddDisasmBoolSetting(() => Settings.SpaceBetweenMemoryAddOperators, value => Settings.SpaceBetweenMemoryAddOperators = value, Instruction.Create(Code.Push_rm64, new MemoryOperand(Register.RBP, Register.RDI, 4, -0x12345678, 8, false, Register.None)));
			SpaceBetweenMemoryMulOperators = AddDisasmBoolSetting(() => Settings.SpaceBetweenMemoryMulOperators, value => Settings.SpaceBetweenMemoryMulOperators = value, Instruction.Create(Code.Push_rm64, new MemoryOperand(Register.RBP, Register.RDI, 4, -0x12345678, 8, false, Register.None)));
			ScaleBeforeIndex = AddDisasmBoolSetting(() => Settings.ScaleBeforeIndex, value => Settings.ScaleBeforeIndex = value, Instruction.Create(Code.Push_rm64, new MemoryOperand(Register.RBP, Register.RDI, 4, -0x12345678, 8, false, Register.None)));
			AlwaysShowScale = AddDisasmBoolSetting(() => Settings.AlwaysShowScale, value => Settings.AlwaysShowScale = value, Instruction.Create(Code.Push_rm64, new MemoryOperand(Register.RBP, Register.RDI, 1, -0x12345678, 8, false, Register.None)));
			AlwaysShowSegmentRegister = AddDisasmBoolSetting(() => Settings.AlwaysShowSegmentRegister, value => Settings.AlwaysShowSegmentRegister = value, Instruction.Create(Code.Push_rm64, new MemoryOperand(Register.RBP, Register.RDI, 4, -0x12345678, 8, false, Register.None)));
			ShowZeroDisplacements = AddDisasmBoolSetting(() => Settings.ShowZeroDisplacements, value => Settings.ShowZeroDisplacements = value, Instruction.Create(Code.Push_rm64, new MemoryOperand(Register.RBP, Register.None, 1, 0, 1, false, Register.None)));
			ShortNumbers = AddDisasmBoolSetting(() => Settings.ShortNumbers, value => Settings.ShortNumbers = value, Instruction.Create(Code.Mov_rm32_imm32, Register.EDI, 0x123));
			ShortBranchNumbers = AddDisasmBoolSetting(() => Settings.ShortBranchNumbers, value => Settings.ShortBranchNumbers = value, Instruction.CreateBranch(Code.Je_rel8_64, 0x12345), false);
			SmallHexNumbersInDecimal = AddDisasmBoolSetting(() => Settings.SmallHexNumbersInDecimal, value => Settings.SmallHexNumbersInDecimal = value, Instruction.Create(Code.Or_rm64_imm8, Register.RDX, 4));
			AddLeadingZeroToHexNumbers = AddDisasmBoolSetting(() => Settings.AddLeadingZeroToHexNumbers, value => Settings.AddLeadingZeroToHexNumbers = value, Instruction.Create(Code.Mov_rm8_imm8, Register.AL, 0xA5));
			SignedImmediateOperands = AddDisasmBoolSetting(() => Settings.SignedImmediateOperands, value => Settings.SignedImmediateOperands = value, Instruction.Create(Code.Or_rm64_imm8, Register.RDX, -0x12));
			SignedMemoryDisplacements = AddDisasmBoolSetting(() => Settings.SignedMemoryDisplacements, value => Settings.SignedMemoryDisplacements = value, Instruction.Create(Code.Push_rm64, new MemoryOperand(Register.RBP, Register.RDI, 4, -0x12345678, 8, false, Register.None)));
			AlwaysShowMemorySize = AddDisasmBoolSetting(() => Settings.MemorySizeOptions == Contracts.Disassembly.MemorySizeOptions.Always, value => Settings.MemorySizeOptions = value ? Contracts.Disassembly.MemorySizeOptions.Always : Contracts.Disassembly.MemorySizeOptions.Default, Instruction.Create(Code.Mov_rm64_r64, new MemoryOperand(Register.RAX, 0, 0), Register.RCX));
			RipRelativeAddresses = AddDisasmBoolSetting(() => Settings.RipRelativeAddresses, value => Settings.RipRelativeAddresses = value, Instruction.Create(Code.Inc_rm64, new MemoryOperand(Register.RIP, Register.None, 1, -0x12345678, 8)));
			ShowBranchSize = AddDisasmBoolSetting(() => Settings.ShowBranchSize, value => Settings.ShowBranchSize = value, Instruction.CreateBranch(Code.Je_rel8_64, X86_RIP + 5));
			UsePseudoOps = AddDisasmBoolSetting(() => Settings.UsePseudoOps, value => Settings.UsePseudoOps = value, Instruction.Create(Code.EVEX_Vcmpps_k_k1_ymm_ymmm256b32_imm8, Register.K3, Register.YMM2, Register.YMM27, 7));
			ShowSymbolAddress = AddDisasmBoolSetting(() => Settings.ShowSymbolAddress, value => Settings.ShowSymbolAddress = value, Instruction.Create(Code.Mov_r64_imm64, Register.RCX, SYMBOLADDR));

			OperandColumnVM = new Int32VM(x86DisassemblySettings.FirstOperandCharIndex + 1, a => {
				if (!OperandColumnVM.HasError)
					this.x86DisassemblySettings.FirstOperandCharIndex = OperandColumnVM.Value - 1;
			}, useDecimal: true) { Min = 1, Max = 100 };

			RefreshDisassembly();
		}

		protected X86DisasmBooleanSetting AddDisasmBoolSetting(Func<bool> getValue, Action<bool> setValue, Instruction instruction, bool fixRip = true) {
			if (fixRip)
				instruction.IP64 = X86_RIP;
			var boolSetting = new X86DisasmBooleanSetting(x86Output, getValue, setValue, formatter, instruction);
			boolSetting.PropertyChanged += DisasmBooleanSetting_PropertyChanged;
			boolSettings.Add(boolSetting);
			return boolSetting;
		}

		void DisasmBooleanSetting_PropertyChanged(object sender, PropertyChangedEventArgs e) {
			if (e.PropertyName == nameof(X86DisasmBooleanSetting.Disassembly))
				return;
			RefreshDisassembly();
		}

		void RefreshDisassembly() {
			InitializeFormatterOptions(formatter.Options);
			foreach (var setting in boolSettings)
				setting.RaiseDisassemblyChanged();
		}

		void InitializeFormatterOptions(FormatterOptions options) {
			InitializeFormatterOptionsCore(options);

			options.UpperCasePrefixes = x86DisassemblySettings.UpperCasePrefixes;
			options.UpperCaseMnemonics = x86DisassemblySettings.UpperCaseMnemonics;
			options.UpperCaseRegisters = x86DisassemblySettings.UpperCaseRegisters;
			options.UpperCaseKeywords = x86DisassemblySettings.UpperCaseKeywords;
			options.UpperCaseOther = x86DisassemblySettings.UpperCaseOther;
			options.UpperCaseAll = x86DisassemblySettings.UpperCaseAll;
			options.FirstOperandCharIndex = x86DisassemblySettings.FirstOperandCharIndex;
			options.TabSize = x86DisassemblySettings.TabSize;
			options.SpaceAfterOperandSeparator = x86DisassemblySettings.SpaceAfterOperandSeparator;
			options.SpaceAfterMemoryBracket = x86DisassemblySettings.SpaceAfterMemoryBracket;
			options.SpaceBetweenMemoryAddOperators = x86DisassemblySettings.SpaceBetweenMemoryAddOperators;
			options.SpaceBetweenMemoryMulOperators = x86DisassemblySettings.SpaceBetweenMemoryMulOperators;
			options.ScaleBeforeIndex = x86DisassemblySettings.ScaleBeforeIndex;
			options.AlwaysShowScale = x86DisassemblySettings.AlwaysShowScale;
			options.AlwaysShowSegmentRegister = x86DisassemblySettings.AlwaysShowSegmentRegister;
			options.ShowZeroDisplacements = x86DisassemblySettings.ShowZeroDisplacements;
			options.HexPrefix = x86DisassemblySettings.HexPrefix;
			options.HexSuffix = x86DisassemblySettings.HexSuffix;
			options.HexDigitGroupSize = x86DisassemblySettings.HexDigitGroupSize;
			options.DecimalPrefix = x86DisassemblySettings.DecimalPrefix;
			options.DecimalSuffix = x86DisassemblySettings.DecimalSuffix;
			options.DecimalDigitGroupSize = x86DisassemblySettings.DecimalDigitGroupSize;
			options.OctalPrefix = x86DisassemblySettings.OctalPrefix;
			options.OctalSuffix = x86DisassemblySettings.OctalSuffix;
			options.OctalDigitGroupSize = x86DisassemblySettings.OctalDigitGroupSize;
			options.BinaryPrefix = x86DisassemblySettings.BinaryPrefix;
			options.BinarySuffix = x86DisassemblySettings.BinarySuffix;
			options.BinaryDigitGroupSize = x86DisassemblySettings.BinaryDigitGroupSize;
			options.DigitSeparator = x86DisassemblySettings.DigitSeparator;
			options.ShortNumbers = x86DisassemblySettings.ShortNumbers;
			options.UpperCaseHex = x86DisassemblySettings.UpperCaseHex;
			options.SmallHexNumbersInDecimal = x86DisassemblySettings.SmallHexNumbersInDecimal;
			options.AddLeadingZeroToHexNumbers = x86DisassemblySettings.AddLeadingZeroToHexNumbers;
			options.NumberBase = UseHexNumbers.Value ? Iced.Intel.NumberBase.Hexadecimal : Iced.Intel.NumberBase.Decimal;
			options.ShortBranchNumbers = x86DisassemblySettings.ShortBranchNumbers;
			options.SignedImmediateOperands = x86DisassemblySettings.SignedImmediateOperands;
			options.SignedMemoryDisplacements = x86DisassemblySettings.SignedMemoryDisplacements;
			options.SignExtendMemoryDisplacements = x86DisassemblySettings.SignExtendMemoryDisplacements;
			options.MemorySizeOptions = DisassemblySettingsUtils.ToMemorySizeOptions(x86DisassemblySettings.MemorySizeOptions);
			options.RipRelativeAddresses = x86DisassemblySettings.RipRelativeAddresses;
			options.ShowBranchSize = x86DisassemblySettings.ShowBranchSize;
			options.UsePseudoOps = x86DisassemblySettings.UsePseudoOps;
			options.ShowSymbolAddress = x86DisassemblySettings.ShowSymbolAddress;

			// The options are only used to show an example so ignore these properties
			options.TabSize = 0;
			options.FirstOperandCharIndex = 0;
		}

		protected abstract void InitializeFormatterOptionsCore(FormatterOptions options);
	}
}
