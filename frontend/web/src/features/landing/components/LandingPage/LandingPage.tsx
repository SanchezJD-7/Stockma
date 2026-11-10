import { type MouseEvent, useLayoutEffect } from 'react'
import '../../landing.css'
import shared from '../../landing.module.css'

import { Comparison } from '../Comparison/Comparison'
import { DemoSection } from '../DemoSection/DemoSection'
import { Faq } from '../Faq/Faq'
import { Features } from '../Features/Features'
import { Footer } from '../Footer/Footer'
import { Header } from '../Header/Header'
import { Hero } from '../Hero/Hero'
import { HowItWorks } from '../HowItWorks/HowItWorks'
import { Pricing } from '../Pricing/Pricing'
import { WaitlistSection } from '../WaitlistSection/WaitlistSection'
import { WhoItsFor } from '../WhoItsFor/WhoItsFor'

export function LandingPage() {
  useLayoutEffect(() => {
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return
    if (typeof window.IntersectionObserver !== 'function') return

    const page = document.getElementById('landing-page')
    if (!page) return

    const sections = page.querySelectorAll<HTMLElement>('main > section')
    const observer = new window.IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) return

          entry.target.setAttribute('data-revealed', 'true')
          observer.unobserve(entry.target)
        })
      },
      { threshold: 0.08 },
    )

    sections.forEach((section) => section.setAttribute('data-reveal', 'true'))
    page.setAttribute('data-reveal-enabled', 'true')
    sections.forEach((section) => observer.observe(section))

    return () => {
      observer.disconnect()
      page.removeAttribute('data-reveal-enabled')
      sections.forEach((section) => {
        section.removeAttribute('data-reveal')
        section.removeAttribute('data-revealed')
      })
    }
  }, [])

  function handleInternalNavigation(event: MouseEvent<HTMLDivElement>) {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return
    }

    const clickedElement = event.target
    if (!(clickedElement instanceof Element)) return

    const anchor = clickedElement.closest<HTMLAnchorElement>('a[href^="#"]')
    const targetId = anchor?.getAttribute('href')?.slice(1)
    if (!targetId) return

    const target = document.getElementById(targetId)
    if (!target) return

    event.preventDefault()

    const demoSection = document.getElementById('demo')
    demoSection?.style.removeProperty('--anchor-scroll-runway')

    if (targetId === 'demo' && demoSection) {
      const scrollPaddingTop = Number.parseFloat(
        window.getComputedStyle(document.documentElement).scrollPaddingTop,
      )
      const headerHeight = document.getElementById('landing-header')?.getBoundingClientRect().height
      const offset = Number.isFinite(scrollPaddingTop) ? scrollPaddingTop : (headerHeight ?? 0)
      const documentHeight = Math.max(
        document.documentElement.scrollHeight,
        document.body.scrollHeight,
      )
      const maxScrollY = documentHeight - window.innerHeight
      const targetDocumentTop = target.getBoundingClientRect().top + window.scrollY
      const scrollRunway = Math.max(0, Math.ceil(targetDocumentTop - offset - maxScrollY))

      if (scrollRunway > 0) {
        demoSection.style.setProperty('--anchor-scroll-runway', `${scrollRunway}px`)
      }
    }

    const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false
    target.scrollIntoView({ behavior: reduceMotion ? 'instant' : 'smooth', block: 'start' })
  }

  return (
    <div id='landing-page' className={shared.page} onClick={handleInternalNavigation}>
      <Header />
      <main>
        <Hero />
        <WhoItsFor />
        <HowItWorks />
        <Features />
        <Comparison />
        <Pricing />
        <Faq />
        <DemoSection />
        <WaitlistSection />
      </main>
      <Footer />
    </div>
  )
}
