"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { post, get } from "@/lib/api";
import type { Company, Professional } from "@/types/models";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";
import { Select } from "@/components/ui/Select";

export default function NewProfessionalPage() {
  const router = useRouter();
  const [name, setName] = useState("");
  const [profileSummary, setProfileSummary] = useState("");
  const [companyId, setCompanyId] = useState("");
  const [companies, setCompanies] = useState<Company[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    get<Company[]>("/companies")
      .then(setCompanies)
      .catch(() => {});
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const professional = await post<Professional>("/professionals", {
        name,
        profile_summary: profileSummary,
        company_id: companyId,
      });
      router.push(`/professionals/${professional._id}`);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to create professional",
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="page-container">
      <h1>Create Professional</h1>

      <div className="form-container">
        {error && <div className="error-message">{error}</div>}

        <form onSubmit={handleSubmit}>
          <FormField
            label="Name"
            name="name"
            type="text"
            value={name}
            onChange={(e) => setName((e.target as HTMLInputElement).value)}
            required
          />

          <FormField
            label="Profile Summary"
            name="profile_summary"
            multiline
            value={profileSummary}
            onChange={(e) =>
              setProfileSummary((e.target as HTMLTextAreaElement).value)
            }
          />

          <Select
            label="Company"
            name="company_id"
            value={companyId}
            onChange={(e) => setCompanyId(e.target.value)}
            options={companies.map((c) => ({ value: c._id, label: c.name }))}
            required
          />

          <div className="form-actions">
            <Button type="submit" loading={loading}>
              Create Professional
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => router.push("/professionals")}
            >
              Cancel
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
