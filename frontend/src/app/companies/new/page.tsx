"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { post } from "@/lib/api";
import type { Company } from "@/types/models";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";

export default function NewCompanyPage() {
  const router = useRouter();
  const [name, setName] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const company = await post<Company>("/companies", { name });
      router.push(`/companies/${company._id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create company");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="page-container">
      <h1>Create Company</h1>

      <div className="form-container">
        {error && <div className="error-message">{error}</div>}

        <form onSubmit={handleSubmit}>
          <FormField
            label="Company Name"
            name="name"
            type="text"
            value={name}
            onChange={(e) => setName((e.target as HTMLInputElement).value)}
            required
          />

          <div className="form-actions">
            <Button type="submit" loading={loading}>
              Create Company
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => router.push("/companies")}
            >
              Cancel
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
