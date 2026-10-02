import { zodResolver } from '@hookform/resolvers/zod'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { LotDto, MaterialDto } from '@/shared/api/types'
import { NativeSelect } from '@/shared/ui/NativeSelect'
import { useMaterials, useRegisterMaterial } from './api'

const QTY_MESSAGE = 'Quantity must be greater than 0'

const schema = z.object({
  materialCode: z.string().min(1, 'Select a material'),
  qty: z.number({ error: QTY_MESSAGE }).positive(QTY_MESSAGE),
})

type FormValues = z.infer<typeof schema>

function RegisterMaterialForm({
  materials,
  onRegistered,
}: {
  materials: MaterialDto[]
  onRegistered: (lot: LotDto) => void
}) {
  const register = useRegisterMaterial()
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { materialCode: '', qty: 1 },
  })
  const { formState } = form
  const materialCode = useWatch({ control: form.control, name: 'materialCode' })
  const uom = materials.find((m) => m.code === materialCode)?.uom

  const submit = form.handleSubmit(async (values) => {
    try {
      onRegistered(await register.mutateAsync(values))
    } catch {
      // The global mutation error toast already reported it; keep the dialog open for a retry.
    }
  })

  return (
    <form className="space-y-4" onSubmit={submit} noValidate>
      <div className="space-y-1.5">
        <Label htmlFor="material">Material</Label>
        <NativeSelect id="material" aria-invalid={!!formState.errors.materialCode} {...form.register('materialCode')}>
          <option value="">Select a material</option>
          {materials.map((m) => (
            <option key={m.code} value={m.code}>
              {m.code} - {m.name}
            </option>
          ))}
        </NativeSelect>
        {formState.errors.materialCode && (
          <p className="text-sm text-destructive">{formState.errors.materialCode.message}</p>
        )}
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="material-qty">Quantity{uom ? ` (${uom})` : ''}</Label>
        <Input
          id="material-qty"
          type="number"
          min={0}
          step="any"
          aria-invalid={!!formState.errors.qty}
          {...form.register('qty', { valueAsNumber: true })}
        />
        {formState.errors.qty && <p className="text-sm text-destructive">{formState.errors.qty.message}</p>}
      </div>

      <div className="flex justify-end">
        <Button type="submit" disabled={register.isPending}>
          Register
        </Button>
      </div>
    </form>
  )
}

interface RegisterMaterialDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onRegistered?: (lot: LotDto) => void
}

export function RegisterMaterialDialog({ open, onOpenChange, onRegistered }: RegisterMaterialDialogProps) {
  const materials = useMaterials()

  function handleRegistered(lot: LotDto) {
    toast.success(`Registered ${lot.lotId}`, { description: `${lot.qty} ${lot.uom} of ${lot.materialCode}` })
    onOpenChange(false)
    onRegistered?.(lot)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Register material</DialogTitle>
          <DialogDescription>Receive raw material or foil into WIP as a new lot.</DialogDescription>
        </DialogHeader>
        {materials.data ? (
          <RegisterMaterialForm materials={materials.data} onRegistered={handleRegistered} />
        ) : (
          <p className="text-sm text-muted-foreground">
            {materials.isError ? 'Could not load materials.' : 'Loading...'}
          </p>
        )}
      </DialogContent>
    </Dialog>
  )
}
