import { zodResolver } from '@hookform/resolvers/zod'
import { useMemo, type ComponentProps } from 'react'
import { useForm, useWatch, type Resolver } from 'react-hook-form'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'
import type {
  CreateWorkOrderRequest,
  EquipmentDto,
  OperationCode,
  ProductDto,
  WorkOrderDto,
} from '@/shared/api/types'

const QTY_MESSAGE = 'Target quantity must be a whole number of at least 1'

interface FormValues {
  productCode: string
  targetQty: number
  plannedStart: string
  plannedEnd: string
  operations: Partial<Record<OperationCode, string>>
}

function buildSchema(route: ProductDto['route']) {
  const operations = Object.fromEntries(
    route.map((step) => [step.operation, z.string().min(1, 'Select equipment')]),
  )
  return z
    .object({
      productCode: z.string().min(1, 'Select a product'),
      targetQty: z.number({ error: QTY_MESSAGE }).int(QTY_MESSAGE).min(1, QTY_MESSAGE),
      plannedStart: z.string().min(1, 'Planned start is required'),
      plannedEnd: z.string().min(1, 'Planned end is required'),
      operations: z.object(operations),
    })
    .refine((v) => !v.plannedStart || !v.plannedEnd || new Date(v.plannedEnd) > new Date(v.plannedStart), {
      path: ['plannedEnd'],
      message: 'Planned end must be after planned start',
    })
}

/** `datetime-local` inputs want local "YYYY-MM-DDTHH:mm", not an ISO instant. */
function toLocalInput(iso: string): string {
  const date = new Date(iso)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

function NativeSelect({ className, ...props }: ComponentProps<'select'>) {
  return (
    <select
      className={cn(
        'h-8 w-full rounded-lg border border-input bg-transparent px-2.5 text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 aria-invalid:border-destructive disabled:cursor-not-allowed disabled:opacity-50',
        className,
      )}
      {...props}
    />
  )
}

function FieldError({ message }: { message?: string }) {
  return message ? <p className="text-sm text-destructive">{message}</p> : null
}

interface WorkOrderFormProps {
  products: ProductDto[]
  equipment: EquipmentDto[]
  /** When set, the form edits this work order and the product is fixed. */
  initial?: WorkOrderDto
  submitting?: boolean
  onSubmit: (request: CreateWorkOrderRequest) => void | Promise<void>
}

export function WorkOrderForm({ products, equipment, initial, submitting, onSubmit }: WorkOrderFormProps) {
  const resolver = useMemo<Resolver<FormValues>>(
    () => (values, context, options) => {
      const route = products.find((p) => p.code === values.productCode)?.route ?? []
      return zodResolver(buildSchema(route))(values, context, options) as ReturnType<Resolver<FormValues>>
    },
    [products],
  )

  const form = useForm<FormValues>({
    resolver,
    defaultValues: {
      productCode: initial?.productCode ?? '',
      targetQty: initial?.targetQty ?? 1,
      plannedStart: initial ? toLocalInput(initial.plannedStart) : '',
      plannedEnd: initial ? toLocalInput(initial.plannedEnd) : '',
      operations: Object.fromEntries((initial?.operations ?? []).map((op) => [op.operation, op.equipmentCode])),
    },
  })
  const { register, formState, control, setValue } = form
  const errors = formState.errors

  const productCode = useWatch({ control, name: 'productCode' })
  const route = useMemo(
    () => [...(products.find((p) => p.code === productCode)?.route ?? [])].sort((a, b) => a.seq - b.seq),
    [products, productCode],
  )

  const submit = form.handleSubmit(async (values) => {
    await onSubmit({
      productCode: values.productCode,
      targetQty: values.targetQty,
      plannedStart: new Date(values.plannedStart).toISOString(),
      plannedEnd: new Date(values.plannedEnd).toISOString(),
      operations: route.map((step) => ({
        operation: step.operation,
        equipmentCode: values.operations[step.operation] ?? '',
      })),
    })
  })

  return (
    <form className="space-y-4" onSubmit={submit} noValidate>
      <div className="space-y-1.5">
        <Label htmlFor="wo-product">Product</Label>
        <NativeSelect
          id="wo-product"
          disabled={!!initial}
          aria-invalid={!!errors.productCode}
          {...register('productCode', { onChange: () => setValue('operations', {}) })}
        >
          <option value="">Select a product</option>
          {products.map((p) => (
            <option key={p.code} value={p.code}>
              {p.code}
            </option>
          ))}
        </NativeSelect>
        <FieldError message={errors.productCode?.message} />
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="wo-qty">Target quantity</Label>
        <Input id="wo-qty" type="number" min={1} step={1} {...register('targetQty', { valueAsNumber: true })} />
        <FieldError message={errors.targetQty?.message} />
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="wo-start">Planned start</Label>
          <Input id="wo-start" type="datetime-local" {...register('plannedStart')} />
          <FieldError message={errors.plannedStart?.message} />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="wo-end">Planned end</Label>
          <Input id="wo-end" type="datetime-local" {...register('plannedEnd')} />
          <FieldError message={errors.plannedEnd?.message} />
        </div>
      </div>

      {route.length > 0 && (
        <fieldset className="space-y-3 rounded-lg border p-3">
          <legend className="px-1 text-sm font-medium">Equipment per step</legend>
          {route.map((step) => {
            const id = `wo-op-${step.operation}`
            const fieldError = errors.operations?.[step.operation]
            return (
              <div key={step.operation} className="space-y-1.5">
                <Label htmlFor={id}>{step.name}</Label>
                <NativeSelect id={id} aria-invalid={!!fieldError} {...register(`operations.${step.operation}`)}>
                  <option value="">Select equipment</option>
                  {equipment
                    .filter((e) => e.operation === step.operation)
                    .map((e) => (
                      <option key={e.code} value={e.code}>
                        {e.code}
                      </option>
                    ))}
                </NativeSelect>
                <FieldError message={fieldError?.message} />
              </div>
            )
          })}
        </fieldset>
      )}

      <div className="flex justify-end">
        <Button type="submit" disabled={submitting || formState.isSubmitting}>
          Save
        </Button>
      </div>
    </form>
  )
}
