"use client";

import { type SelectHTMLAttributes } from "react";

interface SelectOption {
  value: string;
  label: string;
}

interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label: string;
  options: SelectOption[];
  error?: string;
  placeholder?: string;
}

export function Select({
  label,
  options,
  error,
  placeholder = "Select...",
  required,
  ...props
}: SelectProps) {
  const id = props.id || props.name || label.toLowerCase().replace(/\s+/g, "-");

  return (
    <div className="form-field">
      <label htmlFor={id}>
        {label}
        {required && <span className="required"> *</span>}
      </label>
      <select id={id} className={error ? "input-error" : ""} {...props}>
        <option value="">{placeholder}</option>
        {options.map((opt) => (
          <option key={opt.value} value={opt.value}>
            {opt.label}
          </option>
        ))}
      </select>
      {error && <span className="field-error">{error}</span>}
    </div>
  );
}
