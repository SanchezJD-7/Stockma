import { useState, type KeyboardEvent } from 'react'
import { MutedText } from '../../../../styles/components/texts'
import styles from './HeroMockTable.module.css'

type InventoryTabId = 'products' | 'lots' | 'expiring'
type ExpirationStatus = 'normal' | 'warn' | 'danger'

interface InventoryRow {
  name: string
  detail: string
  quantity: string
  fill: 'high' | 'medium' | 'low'
  daysUntilExpiration?: number
  expirationStatus?: ExpirationStatus
}

interface InventoryTab {
  id: InventoryTabId
  label: string
  rows: InventoryRow[]
}

const inventoryTabs: InventoryTab[] = [
  {
    id: 'products',
    label: 'Productos',
    rows: [
      {
        name: 'Yogur griego natural · 1 L',
        detail: 'Lácteos · 2 lotes',
        quantity: '128 u',
        fill: 'high',
      },
      {
        name: 'Crema hidratante · 200 ml',
        detail: 'Cuidado personal · 1 lote',
        quantity: '36 u',
        fill: 'medium',
      },
      {
        name: 'Leche en polvo · 800 g',
        detail: 'Despensa · 3 lotes',
        quantity: '210 u',
        fill: 'high',
      },
    ],
  },
  {
    id: 'lots',
    label: 'Lotes',
    rows: [
      {
        name: 'Yogur griego natural · 1 L',
        detail: 'Lote YG-2408 · vence 12 nov',
        quantity: '48 u',
        fill: 'medium',
      },
      {
        name: 'Crema hidratante · 200 ml',
        detail: 'Lote CR-1102 · vence 14 dic',
        quantity: '36 u',
        fill: 'medium',
      },
      {
        name: 'Leche en polvo · 800 g',
        detail: 'Lote LP-0721 · vence 08 mar',
        quantity: '210 u',
        fill: 'high',
      },
    ],
  },
  {
    id: 'expiring',
    label: 'Por vencer',
    rows: [
      {
        name: 'Yogur griego natural · 1 L',
        detail: 'Lote YG-2408 · 48 unidades',
        quantity: '21 días',
        fill: 'medium',
        daysUntilExpiration: 21,
        expirationStatus: 'normal',
      },
      {
        name: 'Crema hidratante · 200 ml',
        detail: 'Lote CR-1102 · 36 unidades',
        quantity: '9 días',
        fill: 'medium',
        daysUntilExpiration: 9,
        expirationStatus: 'warn',
      },
      {
        name: 'Leche en polvo · 800 g',
        detail: 'Lote LP-0721 · 24 unidades',
        quantity: '3 días',
        fill: 'low',
        daysUntilExpiration: 3,
        expirationStatus: 'danger',
      },
    ],
  },
]

export function HeroMockTable() {
  const [activeTabId, setActiveTabId] = useState<InventoryTabId>('products')

  function handleTabKeyDown(event: KeyboardEvent<HTMLButtonElement>, currentIndex: number) {
    let nextIndex = currentIndex

    if (event.key === 'ArrowRight') nextIndex = (currentIndex + 1) % inventoryTabs.length
    else if (event.key === 'ArrowLeft') {
      nextIndex = (currentIndex - 1 + inventoryTabs.length) % inventoryTabs.length
    } else if (event.key === 'Home') nextIndex = 0
    else if (event.key === 'End') nextIndex = inventoryTabs.length - 1
    else return

    event.preventDefault()
    const nextTab = inventoryTabs[nextIndex]
    setActiveTabId(nextTab.id)
    document.getElementById(`hero-mock-tab-${nextTab.id}`)?.focus()
  }

  return (
    <div className={styles.root}>
      <MutedText className={styles.caption}>
        Modelo de referencia. Así se ve tu inventario: cada producto con sus lotes, cantidades y
        fecha de vencimiento.
      </MutedText>
      <div className={styles.mock} role='region' aria-label='Mock visual de inventario'>
        <div className={styles.heading}>
          <span className={styles.headingTitle}>Inventario · Supermercado Laureles</span>
          <span className={styles.lotCount}>3 lotes por vencer</span>
        </div>
        <div className={styles.tabs} role='tablist' aria-label='Vistas del inventario'>
          {inventoryTabs.map((tab, index) => {
            const isSelected = tab.id === activeTabId

            return (
              <button
                key={tab.id}
                id={`hero-mock-tab-${tab.id}`}
                className={styles.tab}
                type='button'
                role='tab'
                aria-selected={isSelected}
                aria-controls={`hero-mock-panel-${tab.id}`}
                tabIndex={isSelected ? 0 : -1}
                onClick={() => setActiveTabId(tab.id)}
                onKeyDown={(event) => handleTabKeyDown(event, index)}
              >
                {tab.label}
              </button>
            )
          })}
        </div>
        <div className={styles.panels}>
          {inventoryTabs.map((tab) => (
            <div
              key={tab.id}
              id={`hero-mock-panel-${tab.id}`}
              role='tabpanel'
              aria-labelledby={`hero-mock-tab-${tab.id}`}
              hidden={tab.id !== activeTabId}
            >
              <ul>
                {tab.rows.map((row) => (
                  <li key={`${tab.id}-${row.name}`}>
                    <div className={styles.rowDetails}>
                      <strong>{row.name}</strong>
                      <span>{row.detail}</span>
                    </div>
                    {row.daysUntilExpiration ? (
                      <span className={styles.expiryChip} data-status={row.expirationStatus}>
                        {row.daysUntilExpiration} días
                      </span>
                    ) : (
                      <span className={styles.quantity}>{row.quantity}</span>
                    )}
                    <div className={styles.stockTrack} data-fill={row.fill} aria-hidden='true'>
                      <span />
                    </div>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
