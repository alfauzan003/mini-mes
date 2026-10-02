import { useParams } from 'react-router'

/** Stand-in for pages that later tasks build. */
export function PlaceholderPage({ title }: { title: string }) {
  const params = useParams()
  const detail = Object.values(params).join(' / ')
  return (
    <div>
      <h1 className="text-2xl font-semibold">{title}</h1>
      {detail && <p className="mt-1 font-mono text-sm text-muted-foreground">{detail}</p>}
      <p className="mt-4 text-sm text-muted-foreground">This page is not built yet.</p>
    </div>
  )
}
