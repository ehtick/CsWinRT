#pragma once

namespace cswinrt
{
    using namespace std::literals;
    using namespace winmd::reader;

    template <typename...T> struct visit_overload : T... { using T::operator()...; };

    template <typename V, typename...C>
    auto call(V&& variant, C&& ...call)
    {
        return std::visit(visit_overload<C...>{ std::forward<C>(call)... }, std::forward<V>(variant));
    }

    struct writer : indented_writer_base<writer>
    {
        using indented_writer_base::indented_writer_base;

        std::string_view _current_namespace{};
        bool _in_abi_namespace = false;
        bool _in_abi_impl_namespace = false;

        bool _check_platform = false;
        std::string _platform;

        writer(std::string_view current_namespace) :
            _current_namespace(current_namespace)
        {
        }

        void write_begin()
        {
            _in_abi_impl_namespace = settings.component;
            std::string namespacePrefix = settings.component ? "ABI.Impl." : "";
            write(R"(//------------------------------------------------------------------------------
// <auto-generated>
//     This file was generated by cswinrt.exe version %
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WinRT;
using WinRT.Interop;


#pragma warning disable 0169 // warning CS0169: The field '...' is never used
#pragma warning disable 0649 // warning CS0169: Field '...' is never assigned to
#pragma warning disable CA2207, CA1063, CA1033, CA1001, CA2213

namespace %%
{
)", VERSION_STRING, namespacePrefix, _current_namespace);
        }

        void write_end()
        {
            write("}\n");
            _in_abi_impl_namespace = false;
        }

        void write_begin_abi()
        {
            if (!settings.netstandard_compat)
            {
                write("\n#pragma warning disable CA1416");
            }
            write("\n#if !EXCLUDE_ABI_NAMESPACE");
            write("\nnamespace ABI.%\n{\n", _current_namespace);
            _in_abi_namespace = true;
        }

        void write_end_abi()
        {
            write("}\n");
            write("#endif\n");
            if (!settings.netstandard_compat)
            {
                write("#pragma warning restore CA1416\n");
            }
            _in_abi_namespace = false;
        }

        using indented_writer_base<writer>::write;

        struct generic_args
        {
            std::vector<std::vector<type_semantics>> _stack;
            size_t _scope = 0;

            struct args_guard
            {
                explicit args_guard(generic_args* owner = nullptr)
                    : _owner(owner)
                {
                }

                ~args_guard()
                {
                    if (_owner)
                    {
                        _owner->pop();
                    }
                }

                args_guard(args_guard&& other) = delete;
                args_guard& operator=(args_guard const&) = delete;
                args_guard& operator=(args_guard&& other) = delete;
                generic_args* _owner;
            };

            struct scope_guard
            {
                size_t _scope;

                explicit scope_guard(generic_args& owner, size_t scope)
                    : _owner(&owner), _scope(scope)
                {
                    _scope = std::exchange(_owner->_scope, _scope);
                }

                ~scope_guard()
                {
                    if (_owner)
                    {
                        _scope = std::exchange(_owner->_scope, _scope);
                    }
                }

                scope_guard(scope_guard&& other)
                    : _owner(other._owner), _scope(other._scope)
                {
                    other._owner = nullptr;
                }

                scope_guard& operator=(scope_guard const&) = delete;
                scope_guard& operator=(scope_guard&& other) = delete;
                generic_args* _owner;
            };

            [[nodiscard]] auto push(std::pair<GenericParam, GenericParam> const& range)
            {
                if (empty(range))
                {
                    return args_guard{ nullptr };
                }

                _stack.emplace_back(begin(range), end(range));
                return args_guard{ this };
            }

            [[nodiscard]] auto push(generic_type_instance const& type)
            {
                XLANG_ASSERT(!type.generic_args.empty());
                _stack.push_back(type.generic_args);
                return args_guard{ this };
            }

            auto get(uint32_t index)
            {
                size_t scope = _scope > 0 ? _scope - 1 : _stack.size();
                for(size_t i = scope; i > 0; --i)
                {
                    auto&& args = &_stack[i-1];
                    if (index >= args->size())
                    {
                        throw_invalid("Generic index out of range");
                    }
                    
                    auto& semantics = (*args)[index];
                    if(auto gti = std::get_if<generic_type_index>(&semantics))
                    {
                        index = gti->index;
                        continue;
                    }
                    return std::pair{ semantics, scope_guard(*this, i) };
                }
                throw_invalid("No generic arguments");
            }

            void pop()
            {
                _stack.pop_back();
            }
        };

        generic_args _generic_args;

        [[nodiscard]] auto push_generic_params(std::pair<GenericParam, GenericParam> const& range)
        {
            return _generic_args.push(range);
        }

        [[nodiscard]] auto push_generic_args(generic_type_instance const& type)
        {
            return _generic_args.push(type);
        }

        auto get_generic_arg_scope(uint32_t index)
        {
            return _generic_args.get(index);
        }

        auto get_generic_arg(uint32_t index)
        {
            return get_generic_arg_scope(index).first;
        }

        void write_code(std::string_view const& value)
        {
            for (auto&& c : value)
            {
                if (c == '`')
                {
                    return;
                }
                else
                {
                    write(c);
                }
            }
        }

        using generic_type_name_write = std::function<void(writer & w, uint32_t index)>;
        generic_type_name_write write_generic_type_name_custom{};
        struct write_generic_type_name_guard
        {
            writer& _writer;
            generic_type_name_write _current;
            write_generic_type_name_guard(writer& w, generic_type_name_write current) : 
                _writer(w), _current(current)
            {
                std::swap(_current, _writer.write_generic_type_name_custom);
            }
            ~write_generic_type_name_guard()
            {
                std::swap(_current, _writer.write_generic_type_name_custom);
            }
        };

        struct write_platform_guard
        {
            writer& _writer;
            write_platform_guard(writer& w) : _writer(w)
            {
                _writer._check_platform = true;
                _writer._platform = {};
            }
            ~write_platform_guard()
            {
                _writer._check_platform = false;
                _writer._platform = {};
            }
        };
    };

    struct separator
    {
        writer& w;
        std::string_view _separator{ ", " };
        bool first{ true };

        void operator()()
        {
            if (first)
            {
                first = false;
            }
            else
            {
                w.write(_separator);
            }
        }
    };
}