import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import type { CreateWorkOrderRequest, WorkOrderDto } from '@/shared/api/types'
import { useCreateWorkOrder, useEquipment, useProducts, useUpdateWorkOrder } from './api'
import { WorkOrderForm } from './WorkOrderForm'

interface WorkOrderFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** Present when editing; absent when creating. */
  workOrder?: WorkOrderDto
  onSaved?: (workOrder: WorkOrderDto) => void
}

export function WorkOrderFormDialog({ open, onOpenChange, workOrder, onSaved }: WorkOrderFormDialogProps) {
  const products = useProducts()
  const equipment = useEquipment()
  const create = useCreateWorkOrder()
  const update = useUpdateWorkOrder(workOrder?.id ?? '')

  async function handleSubmit(request: CreateWorkOrderRequest) {
    try {
      const saved = workOrder
        ? await update.mutateAsync({
            targetQty: request.targetQty,
            plannedStart: request.plannedStart,
            plannedEnd: request.plannedEnd,
            operations: request.operations,
          })
        : await create.mutateAsync(request)
      onOpenChange(false)
      onSaved?.(saved)
    } catch {
      // The global mutation error toast already reported it; keep the dialog open for a retry.
    }
  }

  const ready = products.data && equipment.data

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{workOrder ? `Edit ${workOrder.number}` : 'New Work Order'}</DialogTitle>
          <DialogDescription>
            {workOrder ? 'Change the target, schedule or equipment.' : 'Pick a product, quantity, schedule and equipment.'}
          </DialogDescription>
        </DialogHeader>
        {ready ? (
          <WorkOrderForm
            products={products.data}
            equipment={equipment.data}
            initial={workOrder}
            submitting={create.isPending || update.isPending}
            onSubmit={handleSubmit}
          />
        ) : (
          <p className="text-sm text-muted-foreground">
            {products.isError || equipment.isError ? 'Could not load products and equipment.' : 'Loading...'}
          </p>
        )}
      </DialogContent>
    </Dialog>
  )
}
