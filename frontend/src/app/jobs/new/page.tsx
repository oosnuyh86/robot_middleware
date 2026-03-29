"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { post, get } from "@/lib/api";
import type { Company, Job } from "@/types/models";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";
import { Select } from "@/components/ui/Select";

export default function NewJobPage() {
  const router = useRouter();
  const [description, setDescription] = useState("");
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
      const job = await post<Job>("/jobs", {
        description,
        company_id: companyId,
      });
      router.push(`/jobs/${job._id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create job");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="page-container">
      <h1>Create Job</h1>

      <div className="form-container">
        {error && <div className="error-message">{error}</div>}

        <form onSubmit={handleSubmit}>
          <FormField
            label="Description"
            name="description"
            multiline
            value={description}
            onChange={(e) =>
              setDescription((e.target as HTMLTextAreaElement).value)
            }
            required
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
              Create Job
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => router.push("/jobs")}
            >
              Cancel
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
