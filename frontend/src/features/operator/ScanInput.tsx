import { useId, useState, type ComponentProps } from 'react'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

interface ScanInputProps {
  label: string
  onScan: (code: string) => void
  disabled?: boolean
}

/**
 * Barcode-wedge friendly field: a scanner types the code and presses Enter. The code is trimmed and
 * upper-cased, the field clears and keeps focus so the next scan can follow immediately.
 */
export function ScanInput({ label, onScan, disabled }: ScanInputProps) {
  const id = useId()
  const [value, setValue] = useState('')

  return (
    <div className="space-y-1.5">
      <Label htmlFor={id} className="text-base">
        {label}
      </Label>
      <Input
        id={id}
        value={value}
        disabled={disabled}
        autoComplete="off"
        autoCapitalize="characters"
        spellCheck={false}
        className="min-h-12 font-mono text-base uppercase"
        onChange={(event) => setValue(event.target.value)}
        onKeyDown={(event) => {
          if (event.key !== 'Enter') return
          event.preventDefault()
          const code = value.trim().toUpperCase()
          if (!code) return
          onScan(code)
          setValue('')
        }}
      />
    </div>
  )
}

type CarrierInputProps = Omit<ComponentProps<'input'>, 'value' | 'onChange' | 'list'> & {
  value: string
  /** Codes of empty carriers, offered as suggestions. The operator can still scan or type any code. */
  suggestions: string[]
  invalid?: boolean
  onChange: (value: string) => void
}

/** Carrier code field: typed or scanned text (upper-cased) with the empty carriers as picks. */
export function CarrierInput({ suggestions, invalid, onChange, value, ...rest }: CarrierInputProps) {
  const listId = useId()
  return (
    <>
      <Input
        {...rest}
        value={value}
        list={listId}
        autoComplete="off"
        spellCheck={false}
        aria-invalid={invalid || undefined}
        placeholder="Scan or pick"
        className="min-h-12 font-mono text-base uppercase"
        onChange={(event) => onChange(event.target.value.toUpperCase())}
        onKeyDown={(event) => {
          // A scanner ends with Enter; it must not submit the surrounding form.
          if (event.key === 'Enter') event.preventDefault()
        }}
      />
      <datalist id={listId}>
        {suggestions.map((code) => (
          <option key={code} value={code} />
        ))}
      </datalist>
    </>
  )
}
