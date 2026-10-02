import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/shared/auth/AuthContext'

const schema = z.object({
  username: z.string().trim().min(1, 'Username is required'),
  password: z.string().min(1, 'Password is required'),
})

type LoginForm = z.infer<typeof schema>

const DEMO_USERS = [
  { username: 'planner', label: 'Planner', description: 'Plans and releases work orders' },
  { username: 'operator', label: 'Operator', description: 'Runs lots at the stations' },
  { username: 'qc', label: 'QC', description: 'Inspects and holds lots' },
  { username: 'admin', label: 'Admin', description: 'Full access' },
]

export function LoginPage() {
  const { user, login, demoLogin } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from ?? '/'

  const form = useForm<LoginForm>({ resolver: zodResolver(schema), defaultValues: { username: '', password: '' } })

  // Failures surface through the global mutation error toast.
  const loginMutation = useMutation({
    mutationFn: (values: LoginForm) => login(values.username, values.password),
    onSuccess: () => navigate(from, { replace: true }),
  })
  const demoMutation = useMutation({
    mutationFn: (username: string) => demoLogin(username),
    onSuccess: () => navigate(from, { replace: true }),
  })

  if (user) return <Navigate to={from} replace />

  const busy = loginMutation.isPending || demoMutation.isPending

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
      <div className="w-full max-w-md space-y-4">
        <Card>
          <CardHeader>
            <CardTitle className="text-xl">Mini MES</CardTitle>
            <CardDescription>Sign in to continue</CardDescription>
          </CardHeader>
          <CardContent>
            <form className="space-y-4" onSubmit={form.handleSubmit((values) => loginMutation.mutate(values))}>
              <div className="space-y-1.5">
                <Label htmlFor="username">Username</Label>
                <Input id="username" autoComplete="username" {...form.register('username')} />
                {form.formState.errors.username && (
                  <p className="text-sm text-destructive">{form.formState.errors.username.message}</p>
                )}
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="password">Password</Label>
                <Input id="password" type="password" autoComplete="current-password" {...form.register('password')} />
                {form.formState.errors.password && (
                  <p className="text-sm text-destructive">{form.formState.errors.password.message}</p>
                )}
              </div>
              <Button type="submit" className="w-full" disabled={busy}>
                Sign in
              </Button>
            </form>
          </CardContent>
        </Card>

        <div className="grid grid-cols-2 gap-3">
          {DEMO_USERS.map((demo) => (
            <button
              key={demo.username}
              type="button"
              disabled={busy}
              onClick={() => demoMutation.mutate(demo.username)}
              className="rounded-lg border bg-card p-3 text-left transition-colors hover:bg-muted disabled:opacity-50"
            >
              <div className="text-sm font-medium">Log in as {demo.label}</div>
              <div className="text-xs text-muted-foreground">{demo.description}</div>
            </button>
          ))}
        </div>
      </div>
    </div>
  )
}
